using IntelliMed.Core.Entities;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers contacts: the Address Book page (people), clinic/hospital workplaces (Clinic.Type), picking an
// existing contact as a referrer, employers as organisation clients (WorkCover/TAC claims), and
// organisation clients having no date of birth.
[Collection("Playwright UI")]
public class ContactsTests : UiTestBase
{
    public ContactsTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private async Task<int> SeedHospitalAsync(string name)
    {
        await using var context = Fixture.OpenDb();
        var hospital = new Clinic { Name = name, Type = ClinicType.Hospital };
        context.Clinics.Add(hospital);
        await context.SaveChangesAsync();
        return hospital.Id;
    }

    private async Task<int> SeedContactAsync(string lastName, string? providerNumber = null)
    {
        await using var context = Fixture.OpenDb();
        var contact = new AddressBookContact { Title = "Dr", FirstName = "Sam", LastName = lastName, ProviderNumber = providerNumber, Speciality = "Cardiology" };
        context.AddressBookContacts.Add(contact);
        await context.SaveChangesAsync();
        return contact.Id;
    }

    private static ILocator ModalField(IPage page, string label) =>
        page.Locator(".modal-content").Locator(
            $"xpath=.//label[normalize-space(text())='{label}']/following-sibling::*[self::input or self::select or self::textarea][1]");

    // ---- Address Book page ---------------------------------------------------------------------

    [Fact]
    public async Task Sidebar_AddressBookLink_OpensTheAddressBook()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Welcome,");

        await page.Locator("a.mdi-nav-link[href='/address-book']").ClickAsync();

        await page.WaitForURLAsync(url => url.EndsWith("/address-book"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Address Book" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AddressBook_NewContact_AtAHospital_IsListedWithItsWorkplace()
    {
        var hospital = UiSeedHelpers.Unique("PW Hospital ");
        var hospitalId = await SeedHospitalAsync(hospital);
        var surname = UiSeedHelpers.Unique("Cardio");

        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/address-book");
        await page.GetByRole(AriaRole.Button, new() { Name = "New Contact" }).ClickAsync();
        await ModalField(page, "Title").FillAsync("Dr");
        await ModalField(page, "First Name").FillAsync("Hana");
        await ModalField(page, "Last Name").FillAsync(surname);
        await ModalField(page, "Profession").FillAsync("Specialist");
        await ModalField(page, "Speciality").FillAsync("Cardiology");
        await ModalField(page, "Provider Number").FillAsync("1234567B");
        await ModalField(page, "Workplace").SelectOptionAsync(new SelectOptionValue { Value = hospitalId.ToString() });
        await page.Locator(".modal-content").GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByText("Contact added.")).ToBeVisibleAsync();
        var row = page.Locator("tr", new() { HasTextString = surname });
        await Assertions.Expect(row).ToContainTextAsync($"Dr Hana {surname}");
        await Assertions.Expect(row).ToContainTextAsync("Specialist · Cardiology");
        await Assertions.Expect(row).ToContainTextAsync("1234567B");
        await Assertions.Expect(row).ToContainTextAsync(hospital);
        await Assertions.Expect(row).ToContainTextAsync("Hospital");
    }

    [Fact]
    public async Task AddressBook_Search_EditAndArchive()
    {
        var surname = UiSeedHelpers.Unique("Findme");
        await SeedContactAsync(surname, "7654321C");

        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/address-book");

        // Search by provider number.
        await page.GetByPlaceholder("Search name, provider number, speciality...").FillAsync("7654321C");
        var row = page.Locator("tr", new() { HasTextString = surname });
        await Assertions.Expect(row).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("tbody tr")).ToHaveCountAsync(1);

        await row.Locator("a.link-primary").ClickAsync();
        await ModalField(page, "Phone").FillAsync("07 3111 2222");
        await page.Locator(".modal-content").GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Contact updated.")).ToBeVisibleAsync();
        await Assertions.Expect(row).ToContainTextAsync("07 3111 2222");

        await row.GetByTitle("Archive").ClickAsync();
        await Assertions.Expect(row).ToHaveCountAsync(0);
        await page.GetByLabel("Show archived").CheckAsync();
        await Assertions.Expect(row).ToContainTextAsync("Archived");
    }

    // ---- Clinic type ---------------------------------------------------------------------------

    [Fact]
    public async Task ClinicManager_ClinicCanBeAHospital()
    {
        var name = UiSeedHelpers.Unique("PW General ");
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/clinic-manager");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Clinic" }).ClickAsync();
        await ModalField(page, "Name").FillAsync(name);
        await ModalField(page, "Type").SelectOptionAsync("Hospital");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Clinic" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Clinic created successfully.")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("tr", new() { HasTextString = name })).ToContainTextAsync("Hospital");
    }

    // ---- Picking contacts from the client record ------------------------------------------------

    [Fact]
    public async Task Referral_PicksAnExistingContact_BySearch()
    {
        var surname = UiSeedHelpers.Unique("Existing");
        await SeedContactAsync(surname, "5550001D");
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.CreateClientAsync(UiSeedHelpers.Unique("Referred"), "Patient");

        await page.TabLink("Referrals").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "+ New" }).ClickAsync();
        await page.Locator(".modal-content", new() { HasTextString = "New Referral" })
            .GetByRole(AriaRole.Button, new() { Name = "Find", Exact = true }).ClickAsync();
        var picker = page.Locator(".modal-content", new() { HasTextString = "Find Referring Provider" });
        await picker.GetByPlaceholder("Search by name, provider number or speciality...").FillAsync(surname);
        var result = picker.Locator(".address-book-result", new() { HasTextString = surname });
        await Assertions.Expect(result).ToContainTextAsync("Cardiology");
        await Assertions.Expect(result).ToContainTextAsync("(5550001D)");
        await result.ClickAsync();

        await Assertions.Expect(page.GetByPlaceholder("Choose from the address book")).ToHaveValueAsync($"Dr Sam {surname}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("tr", new() { HasTextString = surname })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Referral_SaveIsDisabled_UntilAReferrerIsChosen()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.CreateClientAsync(UiSeedHelpers.Unique("NoReferrer"), "Patient");

        await page.TabLink("Referrals").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "+ New" }).ClickAsync();

        await Assertions.Expect(page.Locator(".modal-content", new() { HasTextString = "New Referral" })
            .GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeDisabledAsync();
    }

    [Fact]
    public async Task CompensationClaim_EmployerIsAnExistingOrganisationClient()
    {
        // An organisation created the normal way (a client of type Organisation) is pickable as employer.
        var orgName = UiSeedHelpers.Unique("PW Freight ");
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/clients/add?type=Organisation");
        await page.WaitForSelectorAsync("text=New Client");
        await page.Field("Family/Org").FillAsync(orgName);
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/clients/edit/"));

        await page.CreateClientAsync(UiSeedHelpers.Unique("Injured"), "Worker");
        await page.TabLink("WC/TAC").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "+ New" }).ClickAsync();
        await ModalField(page, "Claim#").FillAsync(UiSeedHelpers.Unique("WC"));
        await page.Locator(".modal-content").GetByRole(AriaRole.Button, new() { Name = "Find", Exact = true }).ClickAsync();
        var picker = page.Locator(".modal-content", new() { HasTextString = "Find Employer" });
        await picker.GetByPlaceholder("Search organisations by name...").FillAsync(orgName);
        await picker.Locator(".organisation-result", new() { HasTextString = orgName }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator("tr", new() { HasTextString = orgName })).ToBeVisibleAsync();
    }

    // ---- Organisation clients have no date of birth ---------------------------------------------

    [Fact]
    public async Task OrganisationClient_HasNoDateOfBirthField_AndSavesWithoutOne()
    {
        var orgName = UiSeedHelpers.Unique("PW Org ");
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/clients/add?type=Organisation");
        await page.WaitForSelectorAsync("text=New Client");

        await Assertions.Expect(page.Locator("label", new() { HasTextString = "D.O.B" })).ToHaveCountAsync(0);
        await page.Field("Family/Org").FillAsync(orgName);
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/clients/edit/"));
        var id = UiSeedHelpers.IdFromUrl(page.Url);

        await using var context = Fixture.OpenDb();
        var saved = await context.Clients.FindAsync(id);
        Assert.Equal(ClientTypeEnum.Organisation, saved!.Type);
        Assert.Null(saved.DateOfBirth);
    }

    [Fact]
    public async Task PersonClient_StillHasADateOfBirthField()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/clients/add");
        await page.WaitForSelectorAsync("text=New Client");

        await Assertions.Expect(page.Field("D.O.B")).ToBeVisibleAsync();
    }
}

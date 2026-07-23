using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

[Collection("Playwright UI")]
public class PatientUpdateTests
{
    private readonly PlaywrightServerFixture _fixture;

    public PatientUpdateTests(PlaywrightServerFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Creates a patient through the real UI flow and returns the page (now on the edit route) plus its new id.</summary>
    private static async Task<(IPage Page, string Id)> CreatePatientAsync(PlaywrightServerFixture fixture, string surname, string given)
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");
        await page.Field("Surname").FillAsync(surname);
        await page.Field("Given").FillAsync(given);
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));

        var match = Regex.Match(page.Url, @"/patients/edit/(\d+)");
        return (page, match.Groups[1].Value);
    }

    [Fact]
    public async Task EditPatient_ReloadingPage_ShowsPreviouslySavedData()
    {
        var (page, id) = await CreatePatientAsync(_fixture, "Reload", "Test");
        await page.Field("Ethnicity").FillAsync("Vietnamese");
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForTimeoutAsync(300);

        await page.GotoAsync($"/patients/edit/{id}");
        await page.WaitForSelectorAsync("text=Edit Patient");

        await Assertions.Expect(page.Field("Surname")).ToHaveValueAsync("Reload");
        await Assertions.Expect(page.Field("Given")).ToHaveValueAsync("Test");
        await Assertions.Expect(page.Field("Ethnicity")).ToHaveValueAsync("Vietnamese");
    }

    [Fact]
    public async Task EditPatient_UpdatingBasicFields_Persists()
    {
        var (page, id) = await CreatePatientAsync(_fixture, "Original", "Name");

        await page.Field("Surname").FillAsync("Updated");
        await page.FieldInCard("Entitlement", "Medicare#").FillAsync("9998887776");
        await page.Field("Email").FillAsync("updated@example.com");
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForTimeoutAsync(300);

        await page.GotoAsync($"/patients/edit/{id}");
        await page.WaitForSelectorAsync("text=Edit Patient");

        await Assertions.Expect(page.Field("Surname")).ToHaveValueAsync("Updated");
        await Assertions.Expect(page.FieldInCard("Entitlement", "Medicare#")).ToHaveValueAsync("9998887776");
        await Assertions.Expect(page.Field("Email")).ToHaveValueAsync("updated@example.com");
    }

    [Fact]
    public async Task EditPatient_UpdatingLifecard_Persists()
    {
        var (page, id) = await CreatePatientAsync(_fixture, "Lifecard", "Holder");

        await page.TabLink("Lifecard").ClickAsync();
        await page.Field("Lifecard#").FillAsync("1111-2222-3333-4444");
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForTimeoutAsync(300);

        await page.GotoAsync($"/patients/edit/{id}");
        await page.WaitForSelectorAsync("text=Edit Patient");
        await page.TabLink("Lifecard").ClickAsync();

        await Assertions.Expect(page.Field("Lifecard#")).ToHaveValueAsync("1111-2222-3333-4444");
    }

    [Fact]
    public async Task ReferralsTab_FullCrudLifecycle()
    {
        var (page, _) = await CreatePatientAsync(_fixture, "Referral", "Owner");

        await page.TabLink("Referrals").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "+ New" }).ClickAsync();

        await page.Field("Referring Provider").FillAsync("Dr Alice Referrer");
        await page.GetByLabel("GP").CheckAsync();
        // Exact = true: the main form's submit button is "💾 Save", which would otherwise also
        // match a plain substring search for "Save".
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByText("Dr Alice Referrer")).ToBeVisibleAsync();

        // Edit it
        await page.GetByRole(AriaRole.Row, new() { NameString = "Dr Alice Referrer" })
            .GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
        await page.Field("Referring Provider").FillAsync("Dr Alice Updated");
        // Exact = true: the main form's submit button is "💾 Save", which would otherwise also
        // match a plain substring search for "Save".
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Dr Alice Updated")).ToBeVisibleAsync();

        // Archive it — disappears from the default (non-archived) view
        await page.GetByRole(AriaRole.Row, new() { NameString = "Dr Alice Updated" })
            .GetByRole(AriaRole.Button, new() { Name = "Archive" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Dr Alice Updated")).Not.ToBeVisibleAsync();

        // Reappears (greyed) once "Include archived" is checked
        await page.GetByLabel("Include archived").CheckAsync();
        await Assertions.Expect(page.GetByText("Dr Alice Updated")).ToBeVisibleAsync();

        // Delete it — gone even with archived included
        await page.GetByRole(AriaRole.Row, new() { NameString = "Dr Alice Updated" })
            .GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Dr Alice Updated")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task OccupationsTab_AddWithHazardFlags_ShowsHazardSummary()
    {
        var (page, _) = await CreatePatientAsync(_fixture, "Occupation", "Worker");

        await page.TabLink("Occupations").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "+ New" }).ClickAsync();

        await page.Field("Occupation").FillAsync("Carpenter");
        await page.Field("Employer").FillAsync("Acme Construction");
        await page.GetByLabel("Asbestos").CheckAsync();
        await page.GetByLabel("Dust").CheckAsync();
        // Exact = true: the main form's submit button is "💾 Save", which would otherwise also
        // match a plain substring search for "Save".
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByText("Carpenter")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Asbestos, Dust")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NewFamily_CreatesLinkedPatientAndShowsInFamilyTab()
    {
        var (page, _) = await CreatePatientAsync(_fixture, "Family", "Head");

        await page.GetByRole(AriaRole.Button, new() { Name = "New Family" }).ClickAsync();
        await page.Field("Surname").Last.FillAsync("Head"); // modal's Surname field (last one on the page)
        await page.Field("Given").Last.FillAsync("Sibling");
        await page.GetByPlaceholder("e.g. Spouse, Child, Parent").FillAsync("Sibling");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        await page.TabLink("Family").ClickAsync();
        await Assertions.Expect(page.GetByText("Sibling Head")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Cell, new() { Name = "Sibling", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PatientVerificationAndPrintLabel_ShowUnderConstructionMessage()
    {
        var (page, _) = await CreatePatientAsync(_fixture, "Construction", "Test");

        await page.GetByRole(AriaRole.Button, new() { Name = "Patient Verification" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Under construction")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "OK" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Print Label" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Under construction")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task UdfTab_WithSeededDefinition_CanAddAndDisplayValue()
    {
        var (page, _) = await CreatePatientAsync(_fixture, "Udf", "Test");

        // No UI exists yet to create UDF definitions (clinic-wide config) — seed one directly
        // via the API, the same way an admin screen would. Enums round-trip as their numeric
        // value over the wire (default System.Text.Json behavior, no string converter
        // configured), so fieldType: 0 here means UdfFieldTypeEnum.Text.
        var api = page.Context.APIRequest;
        var response = await api.PostAsync($"{_fixture.BaseUrl}/api/udf-definitions", new()
        {
            DataObject = new { name = "Preferred Pharmacy", fieldType = 0, displayOrder = 1 }
        });
        Assert.True(response.Ok, $"Failed to seed UDF definition: {response.Status}");

        await page.TabLink("UDF").ClickAsync();
        await page.ReloadAsync();
        await page.TabLink("UDF").ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "+ New" }).ClickAsync();
        await page.Field("Value").FillAsync("Chemist Warehouse");
        // Exact = true: the main form's submit button is "💾 Save", which would otherwise also
        // match a plain substring search for "Save".
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByText("Preferred Pharmacy")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Chemist Warehouse")).ToBeVisibleAsync();
    }
}

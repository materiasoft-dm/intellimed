using IntelliMed.Core.Entities;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers ClinicManager.razor (add, edit, assign users) and the parts of ClinicSettings.razor that
// EmailInfrastructureTests doesn't: practice details and the account-type → fee-schedule mapping.
// Settings changes are restored before each test finishes, since the database is shared.
// Not covered: "Update Fees & Bill Items", which downloads the live MBS catalogue.
[Collection("Playwright UI")]
public class ClinicTests : UiTestBase
{
    public ClinicTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private static ILocator ModalField(IPage page, string label) =>
        page.Locator(".modal-content").Locator(
            $"xpath=.//label[normalize-space(text())='{label}']/following-sibling::*[self::input or self::select or self::textarea][1]");

    private static ILocator ClinicRow(IPage page, string name) =>
        page.Locator("tr", new() { HasTextString = name });

    private static async Task<IPage> OpenClinicManagerAsync(PlaywrightServerFixture fixture)
    {
        var page = await fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/clinic-manager");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Clinic Manager" }).WaitForAsync();
        return page;
    }

    private static async Task<string> AddClinicAsync(IPage page)
    {
        var name = UiSeedHelpers.Unique("PW Clinic ");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Clinic" }).ClickAsync();
        await page.Locator(".modal-title", new() { HasTextString = "Add Clinic" }).WaitForAsync();
        await ModalField(page, "Name").FillAsync(name);
        await ModalField(page, "Phone").FillAsync("07 3000 0000");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Clinic" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Clinic created successfully.")).ToBeVisibleAsync();
        return name;
    }

    // ---- Clinic Manager ------------------------------------------------------------------------

    [Fact]
    public async Task UserMenu_ClinicManager_OpensTheClinicList()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Welcome,");

        await page.Locator("button.dropdown-toggle:has(i.bi-person-circle)").ClickAsync();
        await page.Locator(".dropdown-menu a[href='/clinic-manager']").ClickAsync();

        await page.WaitForURLAsync(url => url.EndsWith("/clinic-manager"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Clinic Manager" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AddClinic_AppearsActive_WithItsPhone()
    {
        var page = await OpenClinicManagerAsync(Fixture);
        var name = await AddClinicAsync(page);

        await Assertions.Expect(ClinicRow(page, name)).ToContainTextAsync("07 3000 0000");
        await Assertions.Expect(ClinicRow(page, name)).ToContainTextAsync("Active");
    }

    [Fact]
    public async Task EditClinic_UpdatesTheList()
    {
        var page = await OpenClinicManagerAsync(Fixture);
        var name = await AddClinicAsync(page);

        await ClinicRow(page, name).GetByRole(AriaRole.Link, new() { Name = name }).ClickAsync();
        await page.Locator(".modal-title", new() { HasTextString = "Edit Clinic" }).WaitForAsync();
        await ModalField(page, "Phone").FillAsync("07 3999 1234");
        await ModalField(page, "Suburb").FillAsync("Fortitude Valley");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Clinic updated successfully.")).ToBeVisibleAsync();
        await Assertions.Expect(ClinicRow(page, name)).ToContainTextAsync("07 3999 1234");
        await Assertions.Expect(ClinicRow(page, name)).ToContainTextAsync("Fortitude Valley");
    }

    [Fact]
    public async Task ManageUsers_AssigningAUser_UpdatesTheClinicsUserCount()
    {
        var page = await OpenClinicManagerAsync(Fixture);
        var email = await page.CreateUserAsync(UiSeedHelpers.Unique("Staff"));
        await page.GotoAsync("/clinic-manager");
        var name = await AddClinicAsync(page);

        await ClinicRow(page, name).GetByTitle("Manage Users").ClickAsync();
        await page.Locator(".modal-title", new() { HasTextString = $"Manage Users — {name}" }).WaitForAsync();
        await page.Locator(".modal-content .form-check", new() { HasTextString = email }).Locator("input").CheckAsync();
        await page.Locator(".modal-content").GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByText("Clinic users updated successfully.")).ToBeVisibleAsync();
        await Assertions.Expect(ClinicRow(page, name).Locator("td").Nth(3)).ToHaveTextAsync("1");
    }

    // ---- Clinic Settings -----------------------------------------------------------------------

    [Fact]
    public async Task ClinicSettings_PracticePhone_SavesAndPersists()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/clinic-settings");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Clinic Settings" }).WaitForAsync();

        var phone = page.Field("Phone");
        await phone.WaitForAsync();
        var original = await phone.InputValueAsync();
        var changed = $"07 {Random.Shared.Next(1000, 9999)} {Random.Shared.Next(1000, 9999)}";
        var save = page.Locator("button[type=submit]", new() { HasTextString = "Save Changes" });

        try
        {
            await phone.FillAsync(changed);
            await save.ClickAsync();
            await Assertions.Expect(page.GetByText("Clinic settings updated successfully.")).ToBeVisibleAsync();

            await page.ReloadAsync();
            await Assertions.Expect(page.Field("Phone")).ToHaveValueAsync(changed);
        }
        finally
        {
            await page.Field("Phone").FillAsync(original);
            await save.ClickAsync();
            await Assertions.Expect(page.GetByText("Clinic settings updated successfully.")).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task ClinicSettings_AccountTypeMapping_SavesAndPersists()
    {
        var code = UiSeedHelpers.Unique("MAP");
        await using (var context = Fixture.OpenDb())
        {
            context.FeeSchedules.Add(new FeeSchedule { Code = code, Description = "Playwright mapping target" });
            await context.SaveChangesAsync();
        }

        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/clinic-settings");
        var card = page.Locator(".card", new() { HasTextString = "Account Type → Fee Schedule" });
        var workCover = card.Locator(".row", new() { HasTextString = "WorkCover" }).First.Locator("select");
        await workCover.WaitForAsync();
        var original = await workCover.InputValueAsync();

        try
        {
            await workCover.SelectOptionAsync(new SelectOptionValue { Label = $"{code} - Playwright mapping target" });
            await Assertions.Expect(page.GetByText("WorkCover mapping saved.")).ToBeVisibleAsync();

            await page.ReloadAsync();
            var reloaded = page.Locator(".card", new() { HasTextString = "Account Type → Fee Schedule" })
                .Locator(".row", new() { HasTextString = "WorkCover" }).First.Locator("select");
            await Assertions.Expect(reloaded.Locator("option:checked")).ToHaveTextAsync($"{code} - Playwright mapping target");
        }
        finally
        {
            await page.Locator(".card", new() { HasTextString = "Account Type → Fee Schedule" })
                .Locator(".row", new() { HasTextString = "WorkCover" }).First.Locator("select")
                .SelectOptionAsync(new SelectOptionValue { Value = original });
        }
    }
}

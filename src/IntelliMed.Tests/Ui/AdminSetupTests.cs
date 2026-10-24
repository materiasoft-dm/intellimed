using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers the set-up pages under /admin that had no UI coverage: User
// Management (edit, deactivate, search), Appointment Types, Command Palette Actions, System
// Settings and Database Backups. Every test uses unique names, and anything that changes shared
// configuration (role permissions, settings, backup schedule) restores it before finishing, since
// the whole "Playwright UI" collection shares one database.
[Collection("Playwright UI")]
public class AdminSetupTests : UiTestBase
{
    public AdminSetupTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..(prefix.Length + 8)];

    private static ILocator Row(IPage page, string text) =>
        page.Locator("tr", new PageLocatorOptions { HasTextString = text });

    // Role Configuration (the permission tree) is covered in RoleConfigurationTests.

    // ---- User Management -----------------------------------------------------------------------

    private static Task<string> CreateUserAsync(IPage page, string lastName) => page.CreateUserAsync(lastName);

    [Fact]
    public async Task UserManagement_EditingAUsersName_UpdatesTheList()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        var lastName = Unique("Edit");
        var email = await CreateUserAsync(page, lastName);

        await Row(page, email).GetByRole(AriaRole.Link, new() { Name = $"Playwright {lastName}" }).ClickAsync();
        await page.WaitForSelectorAsync("text=Edit User");

        var renamed = Unique("Renamed");
        await page.Field("Last Name").FillAsync(renamed);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" }).ClickAsync();

        await Assertions.Expect(page.GetByText("User updated successfully.")).ToBeVisibleAsync();
        await Assertions.Expect(Row(page, email)).ToContainTextAsync($"Playwright {renamed}");
    }

    [Fact]
    public async Task UserManagement_Search_FiltersToMatchingUsers()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        var lastName = Unique("Find");
        var email = await CreateUserAsync(page, lastName);

        await page.GetByPlaceholder("Search users...").FillAsync(lastName);

        await Assertions.Expect(page.Locator("tbody tr")).ToHaveCountAsync(1);
        await Assertions.Expect(Row(page, email)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task UserManagement_DeactivatingAUser_MarksItInactive_AndBlocksSignIn()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        var email = await CreateUserAsync(page, Unique("Gone"));

        // Give it a known password first, so the sign-in attempt below fails for the right reason.
        await Row(page, email).GetByTitle("Reset Password").ClickAsync();
        await page.Locator("input[type='password']").FillAsync(UiUserHelpers.TestUserPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Reset Password" }).ClickAsync();
        await page.WaitForSelectorAsync("text=Password reset successfully.");

        await Row(page, email).GetByTitle("Deactivate").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Deactivate User" }).ClickAsync();
        await Assertions.Expect(page.GetByText("User deactivated successfully.")).ToBeVisibleAsync();
        await Assertions.Expect(Row(page, email)).ToContainTextAsync("Inactive");

        var login = await Fixture.NewPageAsync();
        await login.GotoAsync("/login");
        await login.Locator("#email").FillAsync(email);
        await login.Locator("#password").FillAsync(UiUserHelpers.TestUserPassword);
        await login.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();

        await Assertions.Expect(login.Locator(".login-error-alert")).ToBeVisibleAsync();
        Assert.Contains("/login", login.Url);
    }

    // ---- Appointment Types ---------------------------------------------------------------------

    [Fact]
    public async Task AppointmentTypes_CreateEditArchive_RoundTrip()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin/appointment-types");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Appointment Types" }).WaitForAsync();

        var name = Unique("PW Consult ");
        await page.GetByRole(AriaRole.Button, new() { Name = "New Type" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Appointment Type");
        await page.Field("Name").FillAsync(name);
        await page.Field("Default Duration (minutes)").FillAsync("25");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(Row(page, name)).ToContainTextAsync("25 min");

        await Row(page, name).GetByRole(AriaRole.Link, new() { Name = name }).ClickAsync();
        await page.WaitForSelectorAsync("text=Edit Appointment Type");
        await page.Field("Default Duration (minutes)").FillAsync("40");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Assertions.Expect(Row(page, name)).ToContainTextAsync("40 min");

        await Row(page, name).GetByRole(AriaRole.Button, new() { Name = "Archive" }).ClickAsync();
        await Assertions.Expect(Row(page, name)).ToHaveCountAsync(0);
    }

    // ---- Command Palette Actions ---------------------------------------------------------------

    [Fact]
    public async Task SearchActions_NewAction_IsListed_FoundInThePalette_AndCanBeArchived()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin/search-actions");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Command Palette Actions" }).WaitForAsync();

        var title = Unique("PW Shortcut ");
        var keyword = Unique("pwkw").ToLowerInvariant();
        await page.GetByRole(AriaRole.Button, new() { Name = "New Action" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "New Action" }).WaitForAsync();
        await page.Field("Title").FillAsync(title);
        await page.Field("Keywords").FillAsync(keyword);
        await page.Field("Category").FillAsync("Playwright");
        await page.Field("Target").FillAsync("/fee-schedules");
        // The Bootstrap icon's glyph becomes part of the button's accessible name, so match on the
        // submit button rather than an exact "Save".
        await page.Locator("button[type=submit]", new() { HasTextString = "Save" }).ClickAsync();

        await page.WaitForURLAsync(url => url.EndsWith("/admin/search-actions"));
        await Assertions.Expect(Row(page, title)).ToBeVisibleAsync();

        // A fresh page so the palette fetches the catalogue again rather than reusing a cached list.
        var palettePage = await Fixture.NewAuthenticatedPageAsync();
        await palettePage.GotoAsync("/");
        await palettePage.Locator(".cp-trigger-btn").ClickAsync();
        await palettePage.Locator(".cp-search-input").FillAsync(keyword);
        await Assertions.Expect(palettePage.Locator(".cp-result-title", new() { HasTextString = title })).ToBeVisibleAsync();

        await Row(page, title).GetByTitle("Archive").ClickAsync();
        await Assertions.Expect(page.GetByText($"'{title}' archived.")).ToBeVisibleAsync();
        await Assertions.Expect(Row(page, title)).ToHaveCountAsync(0);
    }

    // ---- System Settings -----------------------------------------------------------------------

    [Fact]
    public async Task SystemSettings_SavingAValue_PersistsAcrossReload()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin/settings");
        await page.GetByRole(AriaRole.Heading, new() { Name = "System Settings" }).WaitForAsync();

        var retention = page.Field("Keep backups for (days)");
        await retention.WaitForAsync();
        var original = await retention.InputValueAsync();
        var changed = original == "61" ? "62" : "61";

        try
        {
            await retention.FillAsync(changed);
            await page.GetByRole(AriaRole.Button, new() { Name = "Save Settings" }).ClickAsync();
            await Assertions.Expect(page.GetByText("Settings saved.")).ToBeVisibleAsync();

            await page.ReloadAsync();
            await Assertions.Expect(page.Field("Keep backups for (days)")).ToHaveValueAsync(changed);
        }
        finally
        {
            await page.Field("Keep backups for (days)").FillAsync(original);
            await page.GetByRole(AriaRole.Button, new() { Name = "Save Settings" }).ClickAsync();
            await Assertions.Expect(page.GetByText("Settings saved.")).ToBeVisibleAsync();
        }
    }

    // ---- Database Backups ----------------------------------------------------------------------

    [Fact]
    public async Task DatabaseBackups_BackupNow_CreatesAManualBackup_ThatDownloadsAndDeletes()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin/database-backups");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Database Backups" }).WaitForAsync();
        await Assertions.Expect(page.GetByText("Loading...")).ToHaveCountAsync(0);

        var before = (await page.Locator("tbody tr td:first-child").AllTextContentsAsync()).ToHashSet();

        await page.GetByRole(AriaRole.Button, new() { Name = "Backup Now" }).ClickAsync();
        await Assertions.Expect(page.Locator("tbody tr")).ToHaveCountAsync(before.Count + 1, new() { Timeout = 15000 });

        var after = await page.Locator("tbody tr td:first-child").AllTextContentsAsync();
        var fileName = after.Single(f => !before.Contains(f));
        var row = Row(page, fileName);
        await Assertions.Expect(row).ToContainTextAsync("Manual");

        var download = await page.RunAndWaitForDownloadAsync(() =>
            row.GetByRole(AriaRole.Button, new() { Name = "Download" }).ClickAsync());
        Assert.Equal(fileName, download.SuggestedFilename);

        // Delete asks via window.confirm; Playwright would otherwise dismiss it (confirm == false).
        page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await row.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Assertions.Expect(row).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task DatabaseBackups_SavingTheSchedule_ShowsSavedAndPersists()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin/database-backups");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Database Backups" }).WaitForAsync();

        var every = page.Field("Back up every");
        var unit = page.Locator(".card", new() { HasTextString = "Schedule" }).Locator("select");
        await every.WaitForAsync();
        var originalEvery = await every.InputValueAsync();
        var originalUnit = await unit.InputValueAsync();

        try
        {
            await every.FillAsync("3");
            await unit.SelectOptionAsync("Weeks");
            await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByText("Saved.", new() { Exact = true })).ToBeVisibleAsync();

            await page.ReloadAsync();
            await Assertions.Expect(page.Field("Back up every")).ToHaveValueAsync("3");
            await Assertions.Expect(page.Locator(".card", new() { HasTextString = "Schedule" }).Locator("select")).ToHaveValueAsync("Weeks");
        }
        finally
        {
            await page.Field("Back up every").FillAsync(originalEvery);
            await page.Locator(".card", new() { HasTextString = "Schedule" }).Locator("select").SelectOptionAsync(originalUnit);
            await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        }
    }
}

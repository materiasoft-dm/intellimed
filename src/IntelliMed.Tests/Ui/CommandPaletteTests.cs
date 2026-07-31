using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers the global command palette (floating search button + Ctrl+K). Regression coverage for a
// real bug found 2026-07-30: the search box placeholder rendered literal "&quot;" instead of a
// quote character, and a failed action-list fetch looked identical to "no matching actions" with
// no way to tell the difference — both fixed in CommandPalette.razor.
[Collection("Playwright UI")]
public class CommandPaletteTests
{
    private readonly PlaywrightServerFixture _fixture;

    public CommandPaletteTests(PlaywrightServerFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task OpenPaletteAsync(IPage page)
    {
        await page.Locator(".cp-trigger-btn").ClickAsync();
        await page.WaitForSelectorAsync(".cp-search-input");
    }

    [Fact]
    public async Task TriggerButton_OpensPaletteWithFullActionListAndCorrectPlaceholder()
    {
        var page = await _fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Dashboard");

        await OpenPaletteAsync(page);

        // Regression: this used to render the literal text "&quot;" instead of a quote character.
        var placeholder = await page.Locator(".cp-search-input").GetAttributeAsync("placeholder");
        Assert.DoesNotContain("&quot;", placeholder);
        Assert.Contains("'patient'", placeholder);

        // The seeded catalogue has ~19 entries — confirms the list actually loaded rather than
        // silently failing (the failure mode this whole regression was about).
        await Assertions.Expect(page.Locator(".cp-result")).Not.ToHaveCountAsync(0);
    }

    [Fact]
    public async Task CtrlK_TogglesPaletteOpenAndClosed()
    {
        var page = await _fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Dashboard");

        await page.Keyboard.PressAsync("Control+K");
        await Assertions.Expect(page.Locator(".cp-panel")).ToBeVisibleAsync();

        await page.Keyboard.PressAsync("Control+K");
        await Assertions.Expect(page.Locator(".cp-panel")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Search_Patient_ReturnsClientActions()
    {
        var page = await _fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Dashboard");
        await OpenPaletteAsync(page);

        await page.Locator(".cp-search-input").FillAsync("patient");

        await Assertions.Expect(page.Locator(".cp-result-title", new() { HasTextString = "Find Client" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".cp-result-title", new() { HasTextString = "Add Client" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Search_Client_ReturnsClientActions()
    {
        // Regression case the user reported: typing "client" (a literal substring of the "Find
        // Client"/"Add Client" titles) returned nothing because the action list had failed to load
        // in their environment — not a matcher bug. Covered here against a known-good, seeded DB.
        var page = await _fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Dashboard");
        await OpenPaletteAsync(page);

        await page.Locator(".cp-search-input").FillAsync("client");

        await Assertions.Expect(page.Locator(".cp-result-title", new() { HasTextString = "Find Client" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".cp-result-title", new() { HasTextString = "Add Client" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Search_Create_ReturnsAllCreateActions()
    {
        var page = await _fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Dashboard");
        await OpenPaletteAsync(page);

        await page.Locator(".cp-search-input").FillAsync("create");

        await Assertions.Expect(page.Locator(".cp-result-title", new() { HasTextString = "Add Client" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".cp-result-title", new() { HasTextString = "New Appointment" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".cp-result-title", new() { HasTextString = "New Invoice" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Search_NonsenseQuery_ShowsNoMatchingActions()
    {
        var page = await _fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Dashboard");
        await OpenPaletteAsync(page);

        await page.Locator(".cp-search-input").FillAsync("xyzzyzzynonsense");

        await Assertions.Expect(page.Locator(".cp-empty", new() { HasTextString = "No matching actions." })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ClickingResult_NavigatesAndClosesPalette()
    {
        var page = await _fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Dashboard");
        await OpenPaletteAsync(page);

        await page.Locator(".cp-search-input").FillAsync("new invoice");
        await page.Locator(".cp-result-title", new PageLocatorOptions { HasTextString = "New Invoice" }).ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/invoices/new"));
        await Assertions.Expect(page.Locator(".cp-panel")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task NonAdminRole_DoesNotSeeAdminOnlyActions()
    {
        // Nurse role has no admin/* page permissions — the palette must filter those out the same
        // way the sidebar nav does. The fixture's database is fresh per test run (only the seeded
        // SuperAdmin exists), so this creates its own Nurse user via the real UI rather than
        // depending on any specific pre-existing account.
        var email = $"nurse-{Guid.NewGuid():N}@example.com";
        const string password = "PlaywrightPass123!";

        var adminPage = await _fixture.NewAuthenticatedPageAsync();
        await adminPage.GotoAsync("/admin/users");
        await adminPage.GetByRole(AriaRole.Heading, new() { Name = "User Management" }).WaitForAsync();

        await adminPage.GetByRole(AriaRole.Button, new() { Name = "Add User" }).ClickAsync();
        await adminPage.Field("First Name").FillAsync("Playwright");
        await adminPage.Field("Last Name").FillAsync("NonAdmin");
        await adminPage.Field("Email Address").FillAsync(email);
        await adminPage.GetByLabel("Nurse").CheckAsync();
        await adminPage.GetByRole(AriaRole.Button, new() { Name = "Create User" }).ClickAsync();
        await adminPage.WaitForSelectorAsync("text=created successfully");
        await adminPage.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();

        // Give the new user a known password via the same admin Reset Password flow used elsewhere.
        await adminPage.Locator("tr", new PageLocatorOptions { HasTextString = email })
            .GetByTitle("Reset Password")
            .ClickAsync();
        await adminPage.Locator("input[type='password']").FillAsync(password);
        await adminPage.GetByRole(AriaRole.Button, new() { Name = "Reset Password" }).ClickAsync();
        await adminPage.WaitForSelectorAsync("text=Password reset successfully.");

        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/login");
        await page.Locator("#email").FillAsync(email);
        await page.Locator("#password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/login"));

        await OpenPaletteAsync(page);
        await page.Locator(".cp-search-input").FillAsync("email templates");

        await Assertions.Expect(page.Locator(".cp-empty", new() { HasTextString = "No matching actions." })).ToBeVisibleAsync();
    }
}

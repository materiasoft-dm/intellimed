using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers the app shell around the feature pages: sign-in failure and sign-out, the unauthenticated
// redirect, the dashboard, the /admin hub (and its RBAC filtering), Help, Profile and not-found.
[Collection("Playwright UI")]
public class ShellAndAuthTests : UiTestBase
{
    public ShellAndAuthTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private static ILocator UserMenuToggle(IPage page) =>
        page.Locator("button.dropdown-toggle:has(i.bi-person-circle)");

    private static ILocator AdminTiles(IPage page) => page.Locator("a > .card");

    // ---- Sign-in / sign-out --------------------------------------------------------------------

    [Fact]
    public async Task Login_WithWrongPassword_ShowsErrorAndStaysOnLoginPage()
    {
        var page = await Fixture.NewPageAsync();
        await page.GotoAsync("/login");
        await page.WaitForSelectorAsync("#email");

        await page.Locator("#email").FillAsync("admin@clinic.com");
        await page.Locator("#password").FillAsync("definitely-not-the-password");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();

        await Assertions.Expect(page.Locator(".login-error-alert")).ToBeVisibleAsync();
        Assert.Contains("/login", page.Url);
    }

    [Fact]
    public async Task Login_ShowsAnAccessNotice_AndNoComplianceBadges()
    {
        // The footer used to claim "HIPAA Compliant" — a US law that doesn't apply here and had never
        // been assessed. Guard against a compliance claim creeping back in without one.
        var page = await Fixture.NewPageAsync();
        await page.GotoAsync("/login");
        await page.WaitForSelectorAsync("#email");

        await Assertions.Expect(page.GetByText("Authorised users only. Patient information is confidential.")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("body")).Not.ToContainTextAsync("HIPAA");
        await Assertions.Expect(page.Locator("body")).Not.ToContainTextAsync("256-bit SSL");
    }

    [Fact]
    public async Task ProtectedPage_WhenSignedOut_RedirectsToLogin()
    {
        var page = await Fixture.NewPageAsync();
        await page.GotoAsync("/invoices");

        await page.WaitForURLAsync(url => url.Contains("/login"));
        await Assertions.Expect(page.GetByText("Welcome Back")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SignOut_FromUserMenu_ReturnsToLogin_AndProtectedPagesRedirectAgain()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Welcome,");

        await UserMenuToggle(page).ClickAsync();
        await page.Locator(".dropdown-menu").GetByRole(AriaRole.Button, new() { Name = "Sign Out" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/login"));

        // AuthService.LogoutAsync blanks the token rather than removing it; IsAuthenticatedAsync treats
        // empty as signed out.
        Assert.True(string.IsNullOrEmpty(await page.EvaluateAsync<string?>("localStorage.getItem('intellimed_token')")));

        await page.GotoAsync("/clients/search");
        await page.WaitForURLAsync(url => url.Contains("/login"));
    }

    // ---- Dashboard -----------------------------------------------------------------------------

    [Fact]
    public async Task Home_ShowsWelcomeAndQuickLinkCards()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("^Welcome,") })).ToBeVisibleAsync();
        foreach (var card in new[] { "Find Client", "Add Client", "Appointments", "Invoices", "Administration" })
        {
            await Assertions.Expect(page.Locator(".card .fw-semibold", new() { HasTextString = card }).First).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task Home_AdministrationCard_OpensTheAdminHub()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Welcome,");

        await page.Locator("main a[href='/admin']").ClickAsync();

        await page.WaitForURLAsync(url => url.EndsWith("/admin"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Administration" })).ToBeVisibleAsync();
    }

    // ---- Administration hub --------------------------------------------------------------------

    [Fact]
    public async Task AdminHub_SuperAdmin_SeesEverySectionAndTile()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Administration" }).WaitForAsync();

        await Assertions.Expect(page.Locator("main h6")).ToHaveTextAsync(new[]
        {
            "Security & Access", "Scheduling", "Communications", "Clinical Reference Data", "System"
        });
        await Assertions.Expect(AdminTiles(page)).ToHaveCountAsync(9);
    }

    [Fact]
    public async Task AdminHub_Search_FiltersTilesByTitleOrDescription()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin");
        await Assertions.Expect(AdminTiles(page)).ToHaveCountAsync(9);

        var search = page.GetByPlaceholder("Search administration pages...");

        await search.FillAsync("backup");
        await Assertions.Expect(AdminTiles(page)).ToHaveCountAsync(1);
        await Assertions.Expect(AdminTiles(page)).ToContainTextAsync("Database Backups");

        // "prescribing" only appears in the Drug Interactions tile's description.
        await search.FillAsync("prescribing");
        await Assertions.Expect(AdminTiles(page)).ToHaveCountAsync(1);
        await Assertions.Expect(AdminTiles(page)).ToContainTextAsync("Drug Interactions");

        await search.FillAsync("zzz-no-such-page");
        await Assertions.Expect(AdminTiles(page)).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByText("No matching administration pages.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminHub_Tile_NavigatesToItsPage()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin");

        await page.Locator("a[href='/admin/roles']").ClickAsync();

        await page.WaitForURLAsync(url => url.EndsWith("/admin/roles"));
    }

    [Fact]
    public async Task Sidebar_AdministrationLink_OpensTheAdminHub()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Welcome,");

        await page.Locator("a.mdi-nav-link[href='/admin']").ClickAsync();

        await page.WaitForURLAsync(url => url.EndsWith("/admin"));
    }

    [Fact]
    public async Task AdminHub_RoleWithNoAdminPages_HasNoSidebarLinkCardOrTiles()
    {
        var page = await Fixture.NewPageSignedInAsRoleAsync("Nurse");
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Welcome,");

        await Assertions.Expect(page.Locator("a.mdi-nav-link[href='/admin']")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("main a[href='/admin']")).ToHaveCountAsync(0);

        // Typing the URL directly still renders the hub, but with nothing the role can open.
        await page.GotoAsync("/admin");
        await Assertions.Expect(page.GetByText("No matching administration pages.")).ToBeVisibleAsync();
        await Assertions.Expect(AdminTiles(page)).ToHaveCountAsync(0);
    }

    // ---- Help, Profile, Not Found --------------------------------------------------------------

    [Fact]
    public async Task Sidebar_HelpLink_OpensTheUserGuide()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Welcome,");

        await page.Locator("a.mdi-nav-link[href='/help']").ClickAsync();

        await page.WaitForURLAsync(url => url.EndsWith("/help"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "User Guide" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task UserMenu_ProfileSettings_ShowsSignedInUsersEmailReadOnly()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Welcome,");

        await UserMenuToggle(page).ClickAsync();
        await page.Locator(".dropdown-menu a[href='/profile']").ClickAsync();
        await page.WaitForURLAsync(url => url.EndsWith("/profile"));

        var email = page.Field("Email");
        await Assertions.Expect(email).ToHaveValueAsync("admin@clinic.com");
        await Assertions.Expect(email).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Profile_SavingAChange_PersistsAcrossReload()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/profile");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Profile Settings" }).WaitForAsync();

        var mobile = $"04{Random.Shared.Next(10000000, 99999999)}";
        await page.Field("Mobile Phone").FillAsync(mobile);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Profile updated successfully.")).ToBeVisibleAsync();

        await page.ReloadAsync();
        await Assertions.Expect(page.Field("Mobile Phone")).ToHaveValueAsync(mobile);
    }

    [Fact]
    public async Task UnknownRoute_RendersTheNotFoundPage()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/definitely-not-a-real-page");

        await Assertions.Expect(page.GetByText("Sorry, the content you are looking for does not exist.")).ToBeVisibleAsync();
    }
}

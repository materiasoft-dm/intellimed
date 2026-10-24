using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Role Configuration shows permissions as a tree — category > group > permission, e.g. Clinical >
// Appointments > View / Create / Update / Remove. Ticking a parent grants everything under it, and a
// parent with only some of its permissions granted shows a partial tick. These tests drive that through
// the real page, then prove it has a real effect (the sidebar a role sees, the admin hub it gets).
//
// Role permissions live in the database the whole "Playwright UI" collection shares, so every test that
// SAVES restores Receptionist's original grants before finishing. Tests that only tick boxes and never
// press Save change nothing.
[Collection("Playwright UI")]
public class RoleConfigurationTests : UiTestBase
{
    private const string Role = "Receptionist";

    // Node ids used by the tree: a leaf is its page key; branches are prefixed.
    private const string Clinical = "category:Clinical";
    private const string Practice = "category:Practice";
    private const string Appointments = "group:Clinical/Appointments";
    private static readonly string[] AppointmentKeys =
        { "appointments", "appointments/create", "appointments/edit", "appointments/delete", "appointments/waiting-room" };

    public RoleConfigurationTests(PlaywrightServerFixture fixture) : base(fixture) { }

    // ---- helpers ------------------------------------------------------------------------------

    private static ILocator Box(IPage page, string nodeId) => page.Locator($"input[id='chk_{nodeId}']");

    private static ILocator Node(IPage page, string nodeId) => page.Locator($"li[data-node='{nodeId}']");

    private static ILocator Count(IPage page, string nodeId) => Node(page, nodeId).Locator("> .perm-row .perm-count");

    private static async Task OpenRoleAsync(IPage page, string role = Role)
    {
        await page.GotoAsync("/admin/roles");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Role Configuration" }).WaitForAsync();
        await page.Field("Select Role").SelectOptionAsync(role);
        await page.Locator("#permissionTree").WaitForAsync();
    }

    private static Task ClearAllAsync(IPage page) =>
        page.GetByRole(AriaRole.Button, new() { Name = "Clear All" }).ClickAsync();

    private static Task SaveAsync(IPage page) =>
        page.GetByRole(AriaRole.Button, new() { Name = "Save Permissions" }).ClickAsync();

    /// <summary>The page keys currently ticked in the tree (leaves only — parents are derived).</summary>
    private static async Task<string[]> TickedKeysAsync(IPage page) =>
        await page.EvaluateAsync<string[]>(
            @"() => [...document.querySelectorAll('#permissionTree input[id^=chk_]:checked')]
                    .map(e => e.id.substring(4))
                    .filter(id => !id.startsWith('group:') && !id.startsWith('category:'))");

    private static async Task ExpectStateAsync(IPage page, string nodeId, string state)
    {
        var box = Box(page, nodeId);
        await Assertions.Expect(box).ToHaveAttributeAsync("data-tri", state);
        switch (state)
        {
            case "on":
                await Assertions.Expect(box).ToBeCheckedAsync();
                break;
            case "mixed":
                // A partial tick is the checkbox's real indeterminate state, not just a CSS class.
                await Assertions.Expect(box).ToBeCheckedAsync(new() { Indeterminate = true });
                break;
            default:
                await Assertions.Expect(box).Not.ToBeCheckedAsync();
                await Assertions.Expect(box).Not.ToBeCheckedAsync(new() { Indeterminate = true });
                break;
        }
    }

    /// <summary>What the database actually holds for Receptionist, sorted.</summary>
    private async Task<string[]> DbKeysAsync()
    {
        await using var db = Fixture.OpenDb();
        return await db.RolePermissions.Where(p => p.RoleName == Role).Select(p => p.PageKey).OrderBy(k => k).ToArrayAsync();
    }

    /// <summary>Puts Receptionist back exactly as it was (the page only saves what is ticked), and
    /// checks the database agrees — a test that leaves shared permissions changed would break others.</summary>
    private async Task RestoreAsync(IPage page, string[] originalKeys, string[] originalDbKeys)
    {
        await OpenRoleAsync(page);
        await ClearAllAsync(page);
        foreach (var key in originalKeys)
        {
            await Box(page, key).CheckAsync();
        }
        await SaveAsync(page);
        await Assertions.Expect(page.GetByText($"Permissions saved for '{Role}'")).ToBeVisibleAsync();
        Assert.Equal(originalDbKeys, await DbKeysAsync());
    }

    // ---- the tree itself ----------------------------------------------------------------------

    [Fact]
    public async Task SelectingARole_ShowsItsCurrentAccess_AsATreeOfParentsAndPermissions()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin/roles");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Role Configuration" }).WaitForAsync();
        await Assertions.Expect(page.GetByText("Select a role above to configure its page access")).ToBeVisibleAsync();

        await page.Field("Select Role").SelectOptionAsync(Role);
        await page.Locator("#permissionTree").WaitForAsync();

        // Regression (fixed 2026-10-22): on a fresh database Program.cs's default seed used to be skipped,
        // because migrations insert a few permission rows first. The fixture's database is fresh, so the
        // seed-only defaults (clients, invoices) must be here alongside the migration-inserted
        // waiting-room key.
        await Assertions.Expect(Box(page, "clients")).ToBeCheckedAsync();
        await Assertions.Expect(Box(page, "invoices")).ToBeCheckedAsync();
        await Assertions.Expect(Box(page, "appointments/waiting-room")).ToBeCheckedAsync();
        await Assertions.Expect(Box(page, "admin/users")).Not.ToBeCheckedAsync();

        // Parents exist: the category, and Appointments under it with the View/Create/Update/Remove pattern.
        await Assertions.Expect(Box(page, Clinical)).ToBeVisibleAsync();
        await Assertions.Expect(Box(page, Appointments)).ToBeVisibleAsync();
        var children = Node(page, Appointments).Locator("> ul > li > .perm-row .perm-label");
        await Assertions.Expect(children).ToContainTextAsync(new[] { "View", "Create", "Update", "Remove", "Waiting room" });

        // Receptionist has some, not all, of Clinical and of Admin (none) — so the parents reflect that.
        await ExpectStateAsync(page, Clinical, "mixed");
        await ExpectStateAsync(page, "category:Admin", "off");
    }

    [Fact]
    public async Task TickingAParent_GrantsEverythingUnderIt_AndLeavesTheRestAlone()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);
        await ClearAllAsync(page);
        await ExpectStateAsync(page, Appointments, "off");

        await Box(page, Appointments).CheckAsync();

        foreach (var key in AppointmentKeys)
            await Assertions.Expect(Box(page, key)).ToBeCheckedAsync();
        await ExpectStateAsync(page, Appointments, "on");
        await Assertions.Expect(Count(page, Appointments)).ToHaveTextAsync("5 of 5");

        // Nothing outside the group changed, so the category above it is only partly ticked.
        await Assertions.Expect(Box(page, "clients")).Not.ToBeCheckedAsync();
        await ExpectStateAsync(page, Clinical, "mixed");
    }

    [Fact]
    public async Task UntickingAParent_RevokesEverythingUnderIt()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);
        await ClearAllAsync(page);
        await Box(page, Appointments).CheckAsync();
        await ExpectStateAsync(page, Appointments, "on");

        await Box(page, Appointments).UncheckAsync();

        foreach (var key in AppointmentKeys)
            await Assertions.Expect(Box(page, key)).Not.ToBeCheckedAsync();
        await ExpectStateAsync(page, Appointments, "off");
        await ExpectStateAsync(page, Clinical, "off");
    }

    [Fact]
    public async Task TickingSomeButNotAllChildren_ShowsTheParentPartlyTicked_ThenFullyTickedOnceTheLastIsTicked()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);
        await ClearAllAsync(page);

        await Box(page, "appointments/create").CheckAsync();

        await ExpectStateAsync(page, Appointments, "mixed");
        await ExpectStateAsync(page, Clinical, "mixed");
        await Assertions.Expect(Count(page, Appointments)).ToHaveTextAsync("1 of 5");

        // Tick all but one: still partial.
        foreach (var key in AppointmentKeys.Where(k => k != "appointments/create" && k != "appointments/delete"))
            await Box(page, key).CheckAsync();
        await ExpectStateAsync(page, Appointments, "mixed");
        await Assertions.Expect(Count(page, Appointments)).ToHaveTextAsync("4 of 5");

        // The last one tips it over.
        await Box(page, "appointments/delete").CheckAsync();
        await ExpectStateAsync(page, Appointments, "on");
        await Assertions.Expect(Count(page, Appointments)).ToHaveTextAsync("5 of 5");
    }

    [Fact]
    public async Task UntickingOneChildOfAFullyTickedParent_MakesItPartlyTicked()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);
        await ClearAllAsync(page);
        await Box(page, Appointments).CheckAsync();
        await ExpectStateAsync(page, Appointments, "on");

        await Box(page, "appointments/delete").UncheckAsync();

        await ExpectStateAsync(page, Appointments, "mixed");
        await Assertions.Expect(Box(page, "appointments")).ToBeCheckedAsync();
        await Assertions.Expect(Count(page, Appointments)).ToHaveTextAsync("4 of 5");
    }

    [Fact]
    public async Task ClickingAPartlyTickedParent_GrantsTheRest()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);
        await ClearAllAsync(page);
        await Box(page, "appointments/create").CheckAsync();
        await ExpectStateAsync(page, Appointments, "mixed");

        await Box(page, Appointments).ClickAsync();

        foreach (var key in AppointmentKeys)
            await Assertions.Expect(Box(page, key)).ToBeCheckedAsync();
        await ExpectStateAsync(page, Appointments, "on");
    }

    [Fact]
    public async Task ACategoryParent_TogglesEveryGroupAndPermissionBeneathIt()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);
        await ClearAllAsync(page);

        await Box(page, Practice).CheckAsync();

        await ExpectStateAsync(page, Practice, "on");
        await Assertions.Expect(Box(page, "clinic-settings")).ToBeCheckedAsync();
        await Assertions.Expect(Box(page, "clinic-manager")).ToBeCheckedAsync();

        // Ticking a whole category reaches through nested groups too.
        await Box(page, Clinical).CheckAsync();
        await ExpectStateAsync(page, Clinical, "on");
        await ExpectStateAsync(page, Appointments, "on");
        await ExpectStateAsync(page, "group:Clinical/Prescriptions", "on");
        await Assertions.Expect(Box(page, "address-book")).ToBeCheckedAsync();

        await Box(page, Clinical).UncheckAsync();
        await ExpectStateAsync(page, Clinical, "off");
        await ExpectStateAsync(page, Appointments, "off");
        await ExpectStateAsync(page, Practice, "on");   // a sibling category is untouched
    }

    [Fact]
    public async Task SelectAllAndClearAll_MoveEveryParentToOnAndOff_AndUpdateTheSummary()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);
        var summary = page.GetByText("pages selected for");

        await page.GetByRole(AriaRole.Button, new() { Name = "Select All" }).ClickAsync();

        foreach (var category in new[] { Clinical, "category:Financial", "category:Admin", Practice, "category:Reports" })
            await ExpectStateAsync(page, category, "on");
        await ExpectStateAsync(page, Appointments, "on");
        var all = await page.Locator("#permissionTree input[id^='chk_']:not([id*=':'])").CountAsync();
        await Assertions.Expect(summary).ToContainTextAsync($"{all} of {all} pages selected");

        await ClearAllAsync(page);

        foreach (var category in new[] { Clinical, "category:Financial", "category:Admin", Practice, "category:Reports" })
            await ExpectStateAsync(page, category, "off");
        await Assertions.Expect(summary).ToContainTextAsync($"0 of {all} pages selected");
    }

    [Fact]
    public async Task Search_NarrowsTheTree_KeepingTheParentsOfWhatMatches()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);

        await page.GetByPlaceholder("Search...").FillAsync("waiting");

        await Assertions.Expect(Box(page, "appointments/waiting-room")).ToBeVisibleAsync();
        await Assertions.Expect(Box(page, Appointments)).ToBeVisibleAsync();      // its parents stay so the match has context
        await Assertions.Expect(Box(page, Clinical)).ToBeVisibleAsync();
        await Assertions.Expect(Box(page, "appointments/create")).ToHaveCountAsync(0);
        await Assertions.Expect(Box(page, "category:Financial")).ToHaveCountAsync(0);

        await page.GetByPlaceholder("Search...").FillAsync("zzz-no-such-page");
        await Assertions.Expect(page.GetByText("No pages match")).ToBeVisibleAsync();

        await page.GetByPlaceholder("Search...").FillAsync("");
        await Assertions.Expect(Box(page, "category:Financial")).ToBeVisibleAsync();
        await Assertions.Expect(Box(page, "appointments/create")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Search_ByGroupName_ShowsAllOfItsPermissions_AndAParentStillReflectsHiddenOnes()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);
        await ClearAllAsync(page);

        // Tick one of the appointment permissions, then narrow the tree to a different one.
        await Box(page, "appointments/create").CheckAsync();
        await page.GetByPlaceholder("Search...").FillAsync("Remove");

        await Assertions.Expect(Box(page, "appointments/delete")).ToBeVisibleAsync();
        await Assertions.Expect(Box(page, "appointments/create")).ToHaveCountAsync(0);
        // The visible parent is still partly ticked, because a hidden permission under it is granted.
        await ExpectStateAsync(page, Appointments, "mixed");
    }

    [Fact]
    public async Task CollapsingAParent_HidesItsPermissions_AndExpandingBringsThemBack()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);
        await Assertions.Expect(Box(page, "appointments/create")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Collapse Appointments" }).ClickAsync();

        await Assertions.Expect(Box(page, "appointments/create")).ToHaveCountAsync(0);
        await Assertions.Expect(Box(page, Appointments)).ToBeVisibleAsync();      // the parent itself stays, with its state
        await Assertions.Expect(Count(page, Appointments)).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Expand Appointments" }).ClickAsync();
        await Assertions.Expect(Box(page, "appointments/create")).ToBeVisibleAsync();

        await page.Locator("#btnCollapseAll").ClickAsync();
        await Assertions.Expect(Box(page, "appointments/create")).ToHaveCountAsync(0);
        await Assertions.Expect(Box(page, "invoices/create")).ToHaveCountAsync(0);
        await Assertions.Expect(Box(page, Appointments)).ToBeVisibleAsync();

        await page.Locator("#btnExpandAll").ClickAsync();
        await Assertions.Expect(Box(page, "appointments/create")).ToBeVisibleAsync();
        await Assertions.Expect(Box(page, "invoices/create")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CollapsingAParent_DoesNotLoseOrChangeWhatIsTickedUnderIt()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);
        await ClearAllAsync(page);
        await Box(page, "appointments/create").CheckAsync();
        await Box(page, "appointments/edit").CheckAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Collapse Appointments" }).ClickAsync();
        await ExpectStateAsync(page, Appointments, "mixed");
        await Assertions.Expect(Count(page, Appointments)).ToHaveTextAsync("2 of 5");
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand Appointments" }).ClickAsync();

        await Assertions.Expect(Box(page, "appointments/create")).ToBeCheckedAsync();
        await Assertions.Expect(Box(page, "appointments/edit")).ToBeCheckedAsync();
        await Assertions.Expect(Box(page, "appointments/delete")).Not.ToBeCheckedAsync();
    }

    // ---- saving, and what it does -------------------------------------------------------------

    [Fact]
    public async Task SavingAGroupGrant_PersistsAsThoseIndividualPermissions_ThroughAReload()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(page);
        var original = await TickedKeysAsync(page);
        var originalDb = await DbKeysAsync();
        try
        {
            await ClearAllAsync(page);
            await Box(page, "group:Financial/Invoices").CheckAsync();   // the whole Invoices group
            await Box(page, "appointments/create").CheckAsync();         // plus one stray permission
            await SaveAsync(page);
            await Assertions.Expect(page.GetByText($"Permissions saved for '{Role}'")).ToBeVisibleAsync();

            await OpenRoleAsync(page);   // fresh load from the server

            await ExpectStateAsync(page, "group:Financial/Invoices", "on");
            foreach (var key in new[] { "invoices", "invoices/create", "invoices/edit", "invoices/delete", "invoices/refund", "invoices/write-off", "invoices/void", "invoices/split" })
                await Assertions.Expect(Box(page, key)).ToBeCheckedAsync();
            await ExpectStateAsync(page, Appointments, "mixed");
            await Assertions.Expect(Box(page, "appointments/create")).ToBeCheckedAsync();
            await Assertions.Expect(Box(page, "appointments")).Not.ToBeCheckedAsync();
            await Assertions.Expect(Box(page, "clients")).Not.ToBeCheckedAsync();
        }
        finally
        {
            await RestoreAsync(page, original, originalDb);
        }
    }

    [Fact]
    public async Task RevokingTheWholeAppointmentsGroup_RemovesAppointmentsFromThatRolesSidebar()
    {
        var admin = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(admin);
        var original = await TickedKeysAsync(admin);
        var originalDb = await DbKeysAsync();
        try
        {
            var before = await Fixture.NewPageSignedInAsRoleAsync(Role);
            await Assertions.Expect(before.Locator("a.mdi-nav-link[href='/appointments']")).ToBeVisibleAsync();

            // Receptionist holds most of Appointments (a partly ticked parent). One tick fills the
            // parent; one untick then takes away all five permissions under it.
            await ExpectStateAsync(admin, Appointments, "mixed");
            await Box(admin, Appointments).ClickAsync();
            await ExpectStateAsync(admin, Appointments, "on");
            await Box(admin, Appointments).UncheckAsync();
            await ExpectStateAsync(admin, Appointments, "off");
            await Assertions.Expect(Box(admin, "clients")).ToBeCheckedAsync();   // nothing else was touched
            await SaveAsync(admin);
            await Assertions.Expect(admin.GetByText($"Permissions saved for '{Role}'")).ToBeVisibleAsync();

            var after = await Fixture.NewPageSignedInAsRoleAsync(Role);
            await Assertions.Expect(after.Locator("a.mdi-nav-link[href='/clients/search']")).ToBeVisibleAsync();   // the rest is intact
            await Assertions.Expect(after.Locator("a.mdi-nav-link[href^='/appointments']")).ToHaveCountAsync(0);
        }
        finally
        {
            await RestoreAsync(admin, original, originalDb);
        }

        var restored = await Fixture.NewPageSignedInAsRoleAsync(Role);
        await Assertions.Expect(restored.Locator("a.mdi-nav-link[href='/appointments']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task GrantingASingleAdminPermission_ShowsItOnThatRolesAdminHub()
    {
        // Receptionist has no admin/* pages by default and no other UI test depends on it.
        var admin = await Fixture.NewAuthenticatedPageAsync();
        await OpenRoleAsync(admin);
        var original = await TickedKeysAsync(admin);
        var originalDb = await DbKeysAsync();
        try
        {
            await Box(admin, "admin/appointment-types").CheckAsync();
            await ExpectStateAsync(admin, "category:Admin", "mixed");   // one of nine admin pages
            await SaveAsync(admin);
            await Assertions.Expect(admin.GetByText($"Permissions saved for '{Role}'")).ToBeVisibleAsync();

            var page = await Fixture.NewPageSignedInAsRoleAsync(Role);
            await page.GotoAsync("/admin");

            var tiles = page.Locator("a > .card");
            await Assertions.Expect(tiles).ToHaveCountAsync(1);
            await Assertions.Expect(tiles).ToContainTextAsync("Appointment Types");
            await Assertions.Expect(page.Locator("a.mdi-nav-link[href='/admin']")).ToBeVisibleAsync();
        }
        finally
        {
            await RestoreAsync(admin, original, originalDb);
        }
    }
}

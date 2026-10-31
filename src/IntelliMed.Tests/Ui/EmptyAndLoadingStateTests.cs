using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Every grid has to say so when it is empty (never a bare table) and say so while it is loading (never a
// false "nothing here" before the rows arrive). Loading is proved by holding the API response back with a
// Playwright route, so the in-flight state stays on screen long enough to assert on.
[Collection("Playwright UI")]
public class EmptyAndLoadingStateTests : UiTestBase
{
    public EmptyAndLoadingStateTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private static readonly TimeSpan ApiDelay = TimeSpan.FromSeconds(2);

    /// <summary>Holds every request whose URL matches the glob back for <see cref="ApiDelay"/> before letting it through.</summary>
    private static Task DelayAsync(IPage page, string urlGlob) =>
        page.RouteAsync(urlGlob, async route =>
        {
            await Task.Delay(ApiDelay);
            await route.ContinueAsync();
        });

    [Theory]
    [InlineData("Referrals", "No referrals recorded.")]
    [InlineData("Allergies", "No allergies recorded.")]
    [InlineData("Prescriptions", "No active prescriptions.")]
    [InlineData("Progress Notes", "No progress notes recorded.")]
    [InlineData("WC/TAC", "No WC/TAC claims recorded.")]
    [InlineData("Family", "No family members linked.")]
    [InlineData("Occupations", "No occupations recorded.")]
    public async Task NewClient_EmptyTab_SaysThereIsNoData_InsteadOfShowingABareTable(string tab, string message)
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        var clientId = await page.CreateClientAsync(UiSeedHelpers.Unique("Empty"), "Patient");

        await page.GotoAsync($"/clients/edit/{clientId}");
        await page.WaitForSelectorAsync("text=Edit Client");
        await page.TabLink(tab).ClickAsync();

        await Assertions.Expect(page.GetByText(message)).ToBeVisibleAsync();
        // The message replaces the table, so there must be no header-only grid left on the tab.
        await Assertions.Expect(page.Locator("table.table-hover")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task PrescriptionsTab_ShowsLoading_WhileTheListIsFetched_ThenTheEmptyMessage()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        var clientId = await page.CreateClientAsync(UiSeedHelpers.Unique("RxLoad"), "Patient");

        await page.GotoAsync($"/clients/edit/{clientId}");
        await page.WaitForSelectorAsync("text=Edit Client");
        await page.TabLink("Prescriptions").ClickAsync();
        await Assertions.Expect(page.GetByText("No active prescriptions.")).ToBeVisibleAsync();

        // Re-fetching with the "include ceased" filter must show Loading, not the old or a false empty message.
        await DelayAsync(page, "**/api/clients/*/prescriptions*");
        await page.GetByLabel("Include ceased/cancelled").CheckAsync();

        await Assertions.Expect(page.GetByText("Loading prescriptions...")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("No prescriptions recorded.")).ToBeHiddenAsync();

        await Assertions.Expect(page.GetByText("Loading prescriptions...")).ToBeHiddenAsync(new() { Timeout = 10_000 });
        await Assertions.Expect(page.GetByText("No prescriptions recorded.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task MedicineSearch_ShowsSearching_NotNoMatches_WhileTheRequestIsInFlight()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        var clientId = await page.CreateClientAsync(UiSeedHelpers.Unique("DrugLoad"), "Patient");
        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");

        await DelayAsync(page, "**/api/medicines/search*");
        await page.GetByPlaceholder("Search by name...").FillAsync("zzzz-not-a-medicine");

        await Assertions.Expect(page.GetByText("Searching...")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("No matches.")).ToBeHiddenAsync();

        await Assertions.Expect(page.GetByText("Searching...")).ToBeHiddenAsync(new() { Timeout = 10_000 });
        await Assertions.Expect(page.GetByText("No matches.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AppointmentTypeSettings_ShowsLoading_BeforeTheList()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();

        await DelayAsync(page, "**/api/appointment-type-settings*");
        await page.GotoAsync("/admin/appointment-types");

        await Assertions.Expect(page.GetByText("Loading appointment types...")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("No appointment types configured yet.")).ToBeHiddenAsync();

        await Assertions.Expect(page.GetByText("Loading appointment types...")).ToBeHiddenAsync(new() { Timeout = 10_000 });
    }
}

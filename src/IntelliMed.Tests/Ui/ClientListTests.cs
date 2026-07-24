using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// NOTE: all tests in the "Playwright UI" collection share one server/database (see
// PlaywrightServerFixture), so the client count keeps growing across the whole suite. Tests here
// deliberately check *relative* counts (before/after creating one client) rather than absolute
// counts, since the database is never actually empty once other test classes have run.
//
// Patients.razor (a separate list page) was consolidated into ClientSearch.razor — these tests
// exercise the search page's results grid and toolbar instead of a standalone list page.
[Collection("Playwright UI")]
public class ClientListTests
{
    private readonly PlaywrightServerFixture _fixture;

    public ClientListTests(PlaywrightServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<int> GetTotalClientCountAsync(IPage page)
    {
        var response = await page.APIRequest.GetAsync($"{_fixture.BaseUrl}/api/clients/search?page=1&pageSize=1");
        var json = await response.JsonAsync();
        return json!.Value.GetProperty("totalCount").GetInt32();
    }

    [Fact]
    public async Task ClientSearch_ShowsRefreshAndNewPersonButtons()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/clients/search");
        await page.WaitForSelectorAsync("text=Client Search");

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Refresh" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "New Person" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NewPersonButton_NavigatesToAddClientPage()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/clients/search");
        await page.WaitForSelectorAsync("text=Client Search");

        await page.GetByRole(AriaRole.Button, new() { Name = "New Person" }).ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/clients/add"));
    }

    [Fact]
    public async Task ClientSearch_AfterCreatingAClient_CountIncreasesByOne()
    {
        var page = await _fixture.NewPageAsync();
        var countBefore = await GetTotalClientCountAsync(page);

        await page.GotoAsync("/clients/add");
        await page.WaitForSelectorAsync("text=New Client");
        await page.Field("Surname").FillAsync("ListVisible");
        await page.Field("Given").FillAsync("Client");
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/clients/edit/"));

        await page.GotoAsync("/clients/search");
        await page.WaitForSelectorAsync("text=Client Search");

        await Assertions.Expect(page.GetByText($"Total Clients: {countBefore + 1}")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ClientSearch_AfterCreatingAClient_ShowsClientRowInGrid()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync("/clients/add");
        await page.WaitForSelectorAsync("text=New Client");
        await page.Field("Surname").FillAsync("GridRow");
        await page.Field("Given").FillAsync("Visible");
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/clients/edit/"));

        await page.GotoAsync("/clients/search");
        await page.WaitForSelectorAsync("text=Client Search");

        // The default unfiltered results page only shows the first 10 clients (by last/first
        // name), and the shared test database accumulates far more than that across the whole
        // suite — filter by surname so the new client is guaranteed to be on the results page.
        await page.Field("Family/Org").FillAsync("GridRow");
        // Exact-name match is ambiguous: the DF Payer field also has its own "Find" button. The
        // main search button is last in DOM order (bottom of the filter panel).
        await page.GetByRole(AriaRole.Button, new() { Name = "Find" }).Last.ClickAsync();

        // Desktop grid and the mobile card view (toggled via CSS, not conditional rendering) both
        // exist in the DOM at once, so the client's name legitimately appears twice.
        await Assertions.Expect(page.GetByText("Visible GridRow").First).ToBeVisibleAsync();
    }
}

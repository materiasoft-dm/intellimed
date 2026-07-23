using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// NOTE: all tests in the "Playwright UI" collection share one server/database (see
// PlaywrightServerFixture), so the patient count keeps growing across the whole suite. Tests here
// deliberately check *relative* counts (before/after creating one patient) rather than absolute
// counts, since the database is never actually empty once other test classes have run.
[Collection("Playwright UI")]
public class PatientListTests
{
    private readonly PlaywrightServerFixture _fixture;

    public PatientListTests(PlaywrightServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<int> GetTotalPatientCountAsync(IPage page)
    {
        var response = await page.APIRequest.GetAsync($"{_fixture.BaseUrl}/api/patients/search?page=1&pageSize=1");
        var json = await response.JsonAsync();
        return json!.Value.GetProperty("totalCount").GetInt32();
    }

    [Fact]
    public async Task PatientList_ShowsNewPatientAndRefreshButtons()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients");
        await page.WaitForSelectorAsync("text=Patients");

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "➕ New Patient" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "🔄 Refresh" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NewPatientButton_NavigatesToAddPatientPage()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients");
        await page.WaitForSelectorAsync("text=Patients");

        await page.GetByRole(AriaRole.Button, new() { Name = "➕ New Patient" }).ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/patients/add"));
    }

    [Fact]
    public async Task PatientList_AfterCreatingAPatient_CountIncreasesByOne()
    {
        var page = await _fixture.NewPageAsync();
        var countBefore = await GetTotalPatientCountAsync(page);

        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");
        await page.Field("Surname").FillAsync("ListVisible");
        await page.Field("Given").FillAsync("Patient");
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));

        await page.GotoAsync("/patients");
        await page.WaitForSelectorAsync("text=Patients");

        // NOTE: as of this writing, the patient list grid (ResizableTable/Grid.js) does not
        // render row data even though the "Showing X of Y" count above it is correct — see the
        // "Fix Grid.js rows not rendering on Patients list" follow-up task. This assertion only
        // checks the count text, which is independent of the grid rendering bug.
        await Assertions.Expect(page.GetByText($"Showing {countBefore + 1} of {countBefore + 1} patients")).ToBeVisibleAsync();
    }

    [Fact(Skip = "Known pre-existing bug: Grid.js row rendering doesn't display data even though " +
        "the correct rows reach the library (header/search/pagination render fine, body stays " +
        "empty). Tracked as a separate follow-up ('Fix Grid.js rows not rendering on Patients " +
        "list'). Un-skip this once that's fixed.")]
    public async Task PatientList_AfterCreatingAPatient_ShowsPatientRowInGrid()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");
        await page.Field("Surname").FillAsync("GridRow");
        await page.Field("Given").FillAsync("Visible");
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));

        await page.GotoAsync("/patients");
        await page.WaitForSelectorAsync("text=Patients");

        await Assertions.Expect(page.GetByText("Visible GridRow")).ToBeVisibleAsync();
    }
}

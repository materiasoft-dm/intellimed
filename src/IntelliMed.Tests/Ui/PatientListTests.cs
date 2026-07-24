using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// NOTE: all tests in the "Playwright UI" collection share one server/database (see
// PlaywrightServerFixture), so the patient count keeps growing across the whole suite. Tests here
// deliberately check *relative* counts (before/after creating one patient) rather than absolute
// counts, since the database is never actually empty once other test classes have run.
//
// Patients.razor (a separate list page) was consolidated into PatientSearch.razor — these tests
// exercise the search page's results grid and toolbar instead of a standalone list page.
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
    public async Task PatientSearch_ShowsRefreshAndNewPersonButtons()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/search");
        await page.WaitForSelectorAsync("text=Patient Search");

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Refresh" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "New Person" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NewPersonButton_NavigatesToAddPatientPage()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/search");
        await page.WaitForSelectorAsync("text=Patient Search");

        await page.GetByRole(AriaRole.Button, new() { Name = "New Person" }).ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/patients/add"));
    }

    [Fact]
    public async Task PatientSearch_AfterCreatingAPatient_CountIncreasesByOne()
    {
        var page = await _fixture.NewPageAsync();
        var countBefore = await GetTotalPatientCountAsync(page);

        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");
        await page.Field("Surname").FillAsync("ListVisible");
        await page.Field("Given").FillAsync("Patient");
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));

        await page.GotoAsync("/patients/search");
        await page.WaitForSelectorAsync("text=Patient Search");

        await Assertions.Expect(page.GetByText($"Total Patients: {countBefore + 1}")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PatientSearch_AfterCreatingAPatient_ShowsPatientRowInGrid()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");
        await page.Field("Surname").FillAsync("GridRow");
        await page.Field("Given").FillAsync("Visible");
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));

        await page.GotoAsync("/patients/search");
        await page.WaitForSelectorAsync("text=Patient Search");

        // The default unfiltered results page only shows the first 10 patients (by last/first
        // name), and the shared test database accumulates far more than that across the whole
        // suite — filter by surname so the new patient is guaranteed to be on the results page.
        await page.Field("Family/Org").FillAsync("GridRow");
        // Exact-name match is ambiguous: the DF Payer field also has its own "Find" button. The
        // main search button is last in DOM order (bottom of the filter panel).
        await page.GetByRole(AriaRole.Button, new() { Name = "Find" }).Last.ClickAsync();

        // Desktop grid and the mobile card view (toggled via CSS, not conditional rendering) both
        // exist in the DOM at once, so the patient's name legitimately appears twice.
        await Assertions.Expect(page.GetByText("Visible GridRow").First).ToBeVisibleAsync();
    }
}

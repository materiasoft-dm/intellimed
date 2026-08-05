using System.Text;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Shares the "Playwright UI" server/database with every other class in this collection, same as
// ClientListTests etc. — each test here uses a distinctively-named medicine so it can't collide
// with data from other test classes or other tests in this file, regardless of execution order.
[Collection("Playwright UI")]
public class MedicinesAdminTests
{
    private readonly PlaywrightServerFixture _fixture;

    public MedicinesAdminTests(PlaywrightServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MedicinesAdminPage_Loads_WithExpectedSections()
    {
        var page = await _fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin/medicines");
        await page.WaitForSelectorAsync("text=Medicine Catalog");

        await Assertions.Expect(page.Locator(".card-header:has-text('Import')")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".card-header:has-text('Enrichment')")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".card-header:has-text('Search')")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Add Medicine" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AddMedicine_Manually_ShowsManualSourceAndIsEditable()
    {
        var page = await _fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin/medicines");
        await page.WaitForSelectorAsync("text=Medicine Catalog");

        await page.GetByRole(AriaRole.Button, new() { Name = "Add Medicine" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Medicine");

        await page.Field("Name").FillAsync("Playwright Handtyped Drug");
        await page.Field("Generic Name").FillAsync("Playwrightamol");
        await page.Field("Active Ingredient(s)").FillAsync("Playwrightamol");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Medicine", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        // Name deliberately avoids containing "Manual"/"Synced" as a substring — GetByText below
        // does substring matching, and a name like "Playwright Manual Drug" would itself match the
        // "Manual" badge locator, causing a Playwright strict-mode ambiguity failure.
        var row = page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright Handtyped Drug" });
        await Assertions.Expect(row).ToBeVisibleAsync();
        await Assertions.Expect(row.GetByText("Manual")).ToBeVisibleAsync();
        await Assertions.Expect(row.GetByRole(AriaRole.Button, new() { Name = "Deactivate" })).ToBeVisibleAsync();

        // Manual entries must be editable: reopening shows Edit (not View) with a Save button, and
        // an edit actually persists.
        await row.GetByRole(AriaRole.Link, new() { Name = "Playwright Handtyped Drug" }).ClickAsync();
        await page.WaitForSelectorAsync("text=Edit Medicine");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save" })).ToBeVisibleAsync();

        await page.Field("Strength").FillAsync("999mg");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await page.WaitForSelectorAsync("text=Edit Medicine", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        await Assertions.Expect(page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright Handtyped Drug" }))
            .ToContainTextAsync("999mg");
    }

    [Fact]
    public async Task ImportCsv_CreatesSyncedMedicine_ThatIsReadOnly()
    {
        var page = await _fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin/medicines");
        await page.WaitForSelectorAsync("text=Medicine Catalog");

        const string csv = "Name,GenericName,Strength,Form,Manufacturer,ActiveIngredients,IsPbsListed,Schedule\n" +
                            "Playwright CsvImport Drug,Playwrightazole,10mg,Tablet,TestCo,Playwrightazole,false,S4\n";

        await page.Locator("input[type=file]").SetInputFilesAsync(new FilePayload
        {
            Name = "playwright-test-import.csv",
            MimeType = "text/csv",
            Buffer = Encoding.UTF8.GetBytes(csv)
        });
        await page.GetByRole(AriaRole.Button, new() { Name = "Import" }).ClickAsync();
        await page.WaitForSelectorAsync("text=1 added");

        // Same "avoid the badge word as a substring" reasoning as the Manual test above.
        var row = page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright CsvImport Drug" });
        await Assertions.Expect(row).ToBeVisibleAsync();
        await Assertions.Expect(row.GetByText("Synced")).ToBeVisibleAsync();
        // Deactivate is Manual-only — a Synced row must not offer it.
        await Assertions.Expect(row.GetByRole(AriaRole.Button, new() { Name = "Deactivate" })).ToHaveCountAsync(0);

        // Synced entries can only be viewed: opening one shows View (not Edit), no Save button, and
        // the fields are genuinely disabled rather than just missing a save action.
        await row.GetByRole(AriaRole.Link, new() { Name = "Playwright CsvImport Drug" }).ClickAsync();
        await page.WaitForSelectorAsync("text=View Medicine");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Close" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Field("Name")).ToBeDisabledAsync();
    }

    [Fact]
    public async Task SourceFilter_ShowsOnlyMatchingRows()
    {
        var page = await _fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin/medicines");
        await page.WaitForSelectorAsync("text=Medicine Catalog");

        await page.GetByRole(AriaRole.Button, new() { Name = "Add Medicine" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Medicine");
        await page.Field("Name").FillAsync("Playwright Filter Drug");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Medicine", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        await page.GetByPlaceholder("Search by name...").FillAsync("Playwright Filter Drug");
        await page.Locator("select").SelectOptionAsync(new SelectOptionValue { Label = "Synced / imported" });
        await Assertions.Expect(page.GetByText("No medicines found.")).ToBeVisibleAsync();

        await page.Locator("select").SelectOptionAsync(new SelectOptionValue { Label = "Manually created" });
        await Assertions.Expect(page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright Filter Drug" }))
            .ToBeVisibleAsync();
    }
}

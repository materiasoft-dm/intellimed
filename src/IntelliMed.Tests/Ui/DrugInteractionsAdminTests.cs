using System.Text;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers DrugInteractionsAdmin.tsx — the reference data behind the prescribing wizard's safety
// check. Each test uses distinctively-named ingredients so rows can't collide with other tests
// sharing this fixture's database.
[Collection("Playwright UI")]
public class DrugInteractionsAdminTests : UiTestBase
{
    public DrugInteractionsAdminTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private async Task<IPage> OpenAdminPageAsync()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/admin/drug-interactions");
        await page.WaitForSelectorAsync("text=Drug Interaction Reference Data");
        return page;
    }

    [Fact]
    public async Task Page_Loads_WithImportAndAddActions()
    {
        var page = await OpenAdminPageAsync();

        await Assertions.Expect(page.Locator(".card-header:has-text('Import / Export')")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Add Interaction" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AddInteraction_Manually_AppearsInTheList()
    {
        var page = await OpenAdminPageAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Add Interaction" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Interaction");

        await page.Field("Ingredient A").FillAsync("Playwrightazine");
        await page.Field("Ingredient B").FillAsync("Playwrightomycin");
        await page.Field("Severity").SelectOptionAsync(new SelectOptionValue { Value = "Major" });
        await page.Field("Description").FillAsync("Risk of QT prolongation.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Interaction", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        var row = page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwrightazine" });
        await Assertions.Expect(row).ToBeVisibleAsync();
        await Assertions.Expect(row).ToContainTextAsync("Major");
    }

    [Fact]
    public async Task AddInteraction_WithoutBothIngredients_ShowsValidationError()
    {
        var page = await OpenAdminPageAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Add Interaction" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Interaction");

        await page.Field("Ingredient A").FillAsync("OnlyOneIngredient");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        // Modal stays open with an error rather than saving a half-formed interaction pair.
        await Assertions.Expect(page.GetByText("New Interaction")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task EditInteraction_LocksTheIngredientPair_ButAllowsSeverityChange()
    {
        var page = await OpenAdminPageAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Add Interaction" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Interaction");
        await page.Field("Ingredient A").FillAsync("Playwrightprofen");
        await page.Field("Ingredient B").FillAsync("Playwrightaxine");
        await page.Field("Severity").SelectOptionAsync(new SelectOptionValue { Value = "Moderate" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Interaction", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        var row = page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwrightprofen" });
        await row.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
        await page.WaitForSelectorAsync("text=Edit Interaction");

        // The pair identifies the row, so it can't be re-pointed at different ingredients.
        await Assertions.Expect(page.Field("Ingredient A")).ToBeDisabledAsync();
        await Assertions.Expect(page.Field("Ingredient B")).ToBeDisabledAsync();

        await page.Field("Severity").SelectOptionAsync(new SelectOptionValue { Value = "Major" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await page.WaitForSelectorAsync("text=Edit Interaction", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        await Assertions.Expect(page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwrightprofen" }))
            .ToContainTextAsync("Major");
    }

    [Fact]
    public async Task ImportCsv_AddsInteractionRows()
    {
        var page = await OpenAdminPageAsync();

        const string csv = "IngredientA,IngredientB,Severity,Description,SourceDataset\n" +
                           "Playwrightolol,Playwrightpine,Moderate,Additive hypotension,playwright-test-set\n";

        await page.Locator("input[type=file]").SetInputFilesAsync(new FilePayload
        {
            Name = "playwright-interactions.csv",
            MimeType = "text/csv",
            Buffer = Encoding.UTF8.GetBytes(csv)
        });
        await page.GetByRole(AriaRole.Button, new() { Name = "Import" }).ClickAsync();

        await Assertions.Expect(page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwrightolol" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
    }
}

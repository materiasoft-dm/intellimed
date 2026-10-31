using IntelliMed.Core.Entities;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers FeeSchedules.razor (schedule CRUD, the Items tab including its paged infinite scroll,
// price history and derived-item overrides) and DerivedItemConfigs.razor. Billing items and
// large schedules are seeded straight into the fixture database: a fresh test database has no MBS
// catalogue, and building 120 items through the picker one at a time would only test the picker.
[Collection("Playwright UI")]
public class FeeScheduleTests : UiTestBase
{
    public FeeScheduleTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private async Task<string> SeedBillingItemAsync(string description, decimal fee)
    {
        var itemNumber = UiSeedHelpers.Unique("PW");
        await using var context = Fixture.OpenDb();
        context.BillingItems.Add(new BillingItem { ItemNumber = itemNumber, Description = description, ScheduleFee = fee, IsActive = true });
        await context.SaveChangesAsync();
        return itemNumber;
    }

    /// <summary>Seeds a schedule holding <paramref name="itemCount"/> items whose numbers sort in
    /// creation order ("{prefix}-001", "{prefix}-002", ...). Returns the schedule code.</summary>
    private async Task<string> SeedScheduleAsync(int itemCount)
    {
        var code = UiSeedHelpers.Unique("FS");
        await using var context = Fixture.OpenDb();
        var schedule = new FeeSchedule { Code = code, Description = $"Playwright {code}" };
        context.FeeSchedules.Add(schedule);
        for (var i = 1; i <= itemCount; i++)
        {
            var item = new BillingItem { ItemNumber = $"{code}-{i:000}", Description = $"Seeded item {i}", ScheduleFee = i, IsActive = true };
            context.BillingItems.Add(item);
            context.FeeScheduleItems.Add(new FeeScheduleItem { FeeSchedule = schedule, BillingItem = item, Fee = i });
        }
        await context.SaveChangesAsync();
        return code;
    }

    private static ILocator ModalField(IPage page, string label) =>
        page.Locator(".modal-content").Locator(
            $"xpath=.//label[normalize-space(text())='{label}']/following-sibling::*[self::input or self::select or self::textarea][1]");

    /// <summary>The "New" button. Its Bootstrap icon glyph is part of the accessible name, so an exact
    /// GetByRole name match fails; textContent excludes the glyph.</summary>
    private static ILocator NewButton(IPage page) =>
        page.Locator("button", new() { HasTextRegex = new System.Text.RegularExpressions.Regex(@"^\s*New\s*$") });

    private static ILocator GridRow(IPage page, string code) =>
        page.Locator(".imed-data-row", new() { HasTextString = code });

    private static async Task<IPage> OpenFeeSchedulesFilteredAsync(PlaywrightServerFixture fixture, string code, bool includeArchived = false)
    {
        var page = await fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/fee-schedules");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Fee Schedules" }).WaitForAsync();
        await page.Field("Code").FillAsync(code);
        if (includeArchived) await page.GetByLabel("Include archived").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Find", Exact = true }).ClickAsync();
        return page;
    }

    private static async Task OpenItemsTabAsync(IPage page, string code)
    {
        await GridRow(page, code).Locator("a.link-primary").ClickAsync();
        await page.WaitForSelectorAsync("text=Edit Fee Schedule");
        await page.Locator(".modal-content a.nav-link", new() { HasTextString = "Items" }).ClickAsync();
    }

    // ---- Schedule CRUD -------------------------------------------------------------------------

    [Fact]
    public async Task NewSchedule_IsCreated_AndFoundByCodeAsActive()
    {
        var code = UiSeedHelpers.Unique("NS");
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/fee-schedules");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Fee Schedules" }).WaitForAsync();

        await NewButton(page).ClickAsync();
        await page.WaitForSelectorAsync("text=New Fee Schedule");
        await ModalField(page, "Code").FillAsync(code);
        await ModalField(page, "Description").FillAsync("Playwright created schedule");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Fee schedule created.")).ToBeVisibleAsync();

        await page.Field("Code").FillAsync(code);
        await page.GetByRole(AriaRole.Button, new() { Name = "Find", Exact = true }).ClickAsync();
        await Assertions.Expect(GridRow(page, code)).ToContainTextAsync("Playwright created schedule");
        await Assertions.Expect(GridRow(page, code)).ToContainTextAsync("Active");
    }

    [Fact]
    public async Task NewSchedule_ItemsTab_AsksToSaveFirst()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/fee-schedules");
        await NewButton(page).ClickAsync();
        await page.Locator(".modal-content a.nav-link", new() { HasTextString = "Items" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Save the schedule first to manage its items.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ArchivingASchedule_HidesItUnlessIncludeArchived_ThenDeleteRemovesIt()
    {
        var code = await SeedScheduleAsync(itemCount: 0);
        var page = await OpenFeeSchedulesFilteredAsync(Fixture, code);

        await GridRow(page, code).Locator("td").Nth(1).ClickAsync(); // select the row, not the code link
        await page.GetByRole(AriaRole.Button, new() { Name = "Archive" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Fee schedule archived.")).ToBeVisibleAsync();
        await Assertions.Expect(GridRow(page, code)).ToHaveCountAsync(0);

        await page.GetByLabel("Include archived").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Find", Exact = true }).ClickAsync();
        await Assertions.Expect(GridRow(page, code)).ToContainTextAsync("Archived");

        // Delete asks via window.confirm; Playwright would otherwise dismiss it.
        page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await GridRow(page, code).Locator("td").Nth(1).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Fee schedule deleted.")).ToBeVisibleAsync();
        await Assertions.Expect(GridRow(page, code)).ToHaveCountAsync(0);
    }

    // ---- Items tab -----------------------------------------------------------------------------

    [Fact]
    public async Task ItemsTab_AddItemViaPicker_EditItsFee_AndViewPriceHistory()
    {
        var itemNumber = await SeedBillingItemAsync("Playwright picker item", 42m);
        var code = await SeedScheduleAsync(itemCount: 0);
        var page = await OpenFeeSchedulesFilteredAsync(Fixture, code);
        await OpenItemsTabAsync(page, code);
        await Assertions.Expect(page.GetByText("No items yet.", new() { Exact = false })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Add Item" }).ClickAsync();
        await page.GetByPlaceholder("Search by item number or description...").FillAsync(itemNumber);
        await page.Locator(".list-group-item", new() { HasTextString = itemNumber }).ClickAsync();

        var row = page.Locator(".modal-content tr", new() { HasTextString = itemNumber });
        await Assertions.Expect(row).ToBeVisibleAsync();

        var fee = row.Locator("input[type=number]");
        await fee.FillAsync("123.45");
        await fee.DispatchEventAsync("change");

        // Reopen the dialog to prove the fee was persisted, not just left in the input.
        await page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
        await OpenItemsTabAsync(page, code);
        await Assertions.Expect(page.Locator(".modal-content tr", new() { HasTextString = itemNumber }).Locator("input[type=number]"))
            .ToHaveValueAsync("123.45");

        await page.Locator(".modal-content tr", new() { HasTextString = itemNumber }).Locator("button.btn-link").ClickAsync();
        await Assertions.Expect(page.GetByText($"Price History", new() { Exact = false })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ItemsTab_LargeSchedule_LoadsFiftyAtATime_AsYouScroll()
    {
        var code = await SeedScheduleAsync(itemCount: 120);
        var page = await OpenFeeSchedulesFilteredAsync(Fixture, code);
        await OpenItemsTabAsync(page, code);

        await Assertions.Expect(page.GetByText("Showing 50 of 120 items.")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#feeScheduleItemsScroll tbody tr")).ToHaveCountAsync(50);

        var scroller = page.Locator("#feeScheduleItemsScroll");
        await scroller.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
        await Assertions.Expect(page.GetByText("Showing 100 of 120 items.")).ToBeVisibleAsync();

        await scroller.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
        await Assertions.Expect(page.GetByText("Showing 120 of 120 items.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("All 120 items loaded.")).ToBeVisibleAsync();

        // Ordered by item number across pages: first and last seeded items both present, once each.
        await Assertions.Expect(page.Locator("#feeScheduleItemsScroll tbody tr").First).ToContainTextAsync($"{code}-001");
        await Assertions.Expect(page.Locator("#feeScheduleItemsScroll tbody tr").Last).ToContainTextAsync($"{code}-120");
    }

    [Fact]
    public async Task DerivedOverrides_SaveGapPercent_AndAddThenRemoveAPrecalculatedRate()
    {
        var code = await SeedScheduleAsync(itemCount: 1);
        var page = await OpenFeeSchedulesFilteredAsync(Fixture, code);
        await OpenItemsTabAsync(page, code);

        await page.Locator(".modal-content tr", new() { HasTextString = $"{code}-001" })
            .GetByRole(AriaRole.Button, new() { Name = "Derived" }).ClickAsync();
        await Assertions.Expect(page.GetByText($"Derived-Item Overrides — {code}-001")).ToBeVisibleAsync();

        var overrides = page.Locator(".modal-content", new() { HasTextString = "Derived-Item Overrides" });
        await overrides.Locator("xpath=.//label[normalize-space(text())='Medical Gap %']/following-sibling::input[1]").FillAsync("10");
        await overrides.GetByRole(AriaRole.Button, new() { Name = "Save Overrides" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Overrides saved.")).ToBeVisibleAsync();

        await overrides.Locator("xpath=.//label[normalize-space(text())='Order# (count/quantity)']/following-sibling::input[1]").FillAsync("2");
        await overrides.Locator("xpath=.//label[normalize-space(text())='Fee ($)']/following-sibling::input[1]").FillAsync("50");
        await overrides.GetByRole(AriaRole.Button, new() { Name = "Add", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Rate added.")).ToBeVisibleAsync();

        // The modal refreshes just this item (GET items/{id}) — the new rate shows without reopening.
        await Assertions.Expect(overrides.Locator("table")).ToContainTextAsync("50.00");
        var rateRow = overrides.Locator("tbody tr", new() { HasTextString = "50.00" });
        await Assertions.Expect(rateRow).ToContainTextAsync("2");

        await rateRow.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Rate removed.")).ToBeVisibleAsync();
        await Assertions.Expect(rateRow).ToHaveCountAsync(0);
    }

    // ---- Derived Item Rules --------------------------------------------------------------------

    [Fact]
    public async Task DerivedItemRules_CreateEditDelete_RoundTrip()
    {
        var itemNumber = await SeedBillingItemAsync("Playwright assistant item", 0m);
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/derived-item-configs");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Derived Item Rules" }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Add Rule" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Derived Item Rule");
        await page.Locator(".modal-content").GetByPlaceholder("Search by item number or description...").First.FillAsync(itemNumber);
        await page.Locator(".modal-content .list-group-item", new() { HasTextString = itemNumber }).ClickAsync();

        await ModalField(page, "Calculation Type").SelectOptionAsync(new SelectOptionValue { Label = "Percentage of Associated Item" });
        await page.Locator(".modal-content label[for=assocModeGroup]").ClickAsync();
        await ModalField(page, "MBS Group Code").FillAsync("T8");
        await ModalField(page, "Percentage (%)").FillAsync("25");
        await page.Locator(".modal-content").GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByText("Rule created.")).ToBeVisibleAsync();
        var row = page.Locator("tr", new() { HasTextString = itemNumber });
        await Assertions.Expect(row).ToContainTextAsync("% of Associated Item");
        await Assertions.Expect(row).ToContainTextAsync("Any MBS Group T8");
        // Value column: index 4. Exact match — regression for the stored decimal rendering as "25.0%".
        await Assertions.Expect(row.Locator("td").Nth(4)).ToHaveTextAsync("25%");

        await row.GetByRole(AriaRole.Link, new() { Name = itemNumber }).ClickAsync();
        await page.WaitForSelectorAsync("text=Edit Derived Item Rule");
        await ModalField(page, "Percentage (%)").FillAsync("30");
        await page.Locator(".modal-content").GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Rule updated.")).ToBeVisibleAsync();
        await Assertions.Expect(row.Locator("td").Nth(4)).ToHaveTextAsync("30%");

        page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await row.Locator("button.btn-outline-danger").ClickAsync();
        await Assertions.Expect(page.GetByText("Rule deleted.")).ToBeVisibleAsync();
        await Assertions.Expect(row).ToHaveCountAsync(0);
    }
}

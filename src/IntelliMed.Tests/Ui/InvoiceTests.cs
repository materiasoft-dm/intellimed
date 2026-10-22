using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers the invoice lifecycle end to end: AddInvoice.razor (validation, custom lines),
// InvoiceDetail.razor (receipts, write-off, cancel, claim status), SplitInvoice.razor,
// InvoiceSearch.razor, InvoicePayments.razor and the two print pages. Invoices use custom lines so
// no fee schedule or MBS catalogue is needed. Amounts are matched without a currency symbol
// because the symbol depends on the browser culture.
[Collection("Playwright UI")]
public class InvoiceTests : UiTestBase
{
    public InvoiceTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private record Invoice(IPage Page, int Id, string ClientName, string Surname);

    /// <summary>Creates a client, then an invoice for them with one custom line per (description,
    /// fee) pair, and returns once the invoice detail page has loaded.</summary>
    private async Task<Invoice> CreateInvoiceAsync(params (string Description, decimal Fee)[] lines)
    {
        var surname = UiSeedHelpers.Unique("Inv");
        var page = await Fixture.NewAuthenticatedPageAsync();
        var clientId = await page.CreateClientAsync(surname, "Billed");

        await page.GotoAsync($"/invoices/new?clientId={clientId}");
        await Assertions.Expect(page.GetByPlaceholder("No client selected")).ToHaveValueAsync($"Billed {surname}");

        foreach (var (description, fee) in lines)
        {
            await page.Locator("button", new() { HasTextString = "Add Custom Line" }).ClickAsync();
            var row = page.Locator("tbody tr").Last;
            await row.Locator("input:not([type])").First.FillAsync(description);
            var feeInput = row.Locator("input[type=number]").Nth(1); // Qty, Fee, Discount, D.Qty
            await feeInput.FillAsync(fee.ToString("0.00"));
            await feeInput.PressAsync("Tab");
        }

        await ClickSaveAsync(page);
        await page.WaitForURLAsync(url => url.Contains("/invoices/edit/"));
        await Assertions.Expect(page.Locator("h5", new() { HasTextString = $"Billed {surname}" })).ToBeVisibleAsync();
        return new Invoice(page, UiSeedHelpers.IdFromUrl(page.Url), $"Billed {surname}", surname);
    }

    /// <summary>A real pointer click on the New Invoice page's Save — no longer covered by the floating
    /// command-palette button (see Save_IsNotCoveredByTheFloatingSearchButton).</summary>
    private static Task ClickSaveAsync(IPage page) =>
        page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();

    /// <summary>The value cell next to a label in the detail page's Details/Status cards.</summary>
    private static ILocator Value(IPage page, string label) =>
        page.Locator($"xpath=//div[contains(@class,'text-muted') and normalize-space()='{label}']/following-sibling::div[1]");

    private static async Task RecordReceiptAsync(IPage page, decimal? amount = null, string reference = "")
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "+ Record Receipt" }).ClickAsync();
        var modal = page.Locator(".modal-content", new() { HasTextString = "Record Receipt" });
        await modal.WaitForAsync();
        if (amount is { } a)
        {
            var allocation = modal.Locator("tbody tr").First.Locator("input[type=number]");
            await allocation.FillAsync(a.ToString("0.00"));
            await allocation.PressAsync("Tab");
            await modal.Locator("xpath=.//label[normalize-space(text())='Amount Tendered']/following-sibling::input[1]").FillAsync(a.ToString("0.00"));
        }
        if (reference.Length > 0)
        {
            await modal.Locator("xpath=.//label[normalize-space(text())='Reference']/following-sibling::input[1]").FillAsync(reference);
        }
        await modal.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Receipt recorded.")).ToBeVisibleAsync();
    }

    // ---- New invoice ---------------------------------------------------------------------------

    [Fact]
    public async Task NewInvoice_WithoutAClient_AsksForOneFirst()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/invoices/new");
        await ClickSaveAsync(page);

        await Assertions.Expect(page.GetByText("Select a client first.")).ToBeVisibleAsync();
        Assert.Contains("/invoices/new", page.Url);
    }

    [Fact]
    public async Task NewInvoice_WithoutLines_AsksForAtLeastOne()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        var clientId = await page.CreateClientAsync(UiSeedHelpers.Unique("NoLine"), "Empty");
        await page.GotoAsync($"/invoices/new?clientId={clientId}");
        await Assertions.Expect(page.GetByPlaceholder("No client selected")).Not.ToHaveValueAsync("");

        await ClickSaveAsync(page);

        await Assertions.Expect(page.GetByText("Add at least one line item.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Save_IsNotCoveredByTheFloatingSearchButton()
    {
        // Regression (fixed 2026-10-22): with a short invoice, the bottom-right Save sat under the fixed
        // command-palette button, so a real click hit the palette instead. .mdi-main-content now
        // reserves bottom padding for the button.
        var page = await Fixture.NewAuthenticatedPageAsync();
        var clientId = await page.CreateClientAsync(UiSeedHelpers.Unique("Fab"), "Covered");
        await page.GotoAsync($"/invoices/new?clientId={clientId}");
        await page.Locator("button", new() { HasTextString = "Add Custom Line" }).ClickAsync();

        // Scroll the page's real scroll container (the main content pane) all the way down — the
        // position where the overlap used to happen.
        var save = page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" });
        await save.ScrollIntoViewIfNeededAsync();
        await page.EvaluateAsync("document.querySelector('.mdi-main-content').scrollTop = 1e6");

        var topElementIsSave = await save.EvaluateAsync<bool>(@"el => {
            const r = el.getBoundingClientRect();
            const top = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
            return el === top || el.contains(top);
        }");
        Assert.True(topElementIsSave, "The floating command-palette button covers the invoice Save button.");
    }

    [Fact]
    public async Task NewInvoice_WithCustomLines_SavesWithTheirTotal()
    {
        var invoice = await CreateInvoiceAsync(("Playwright consult", 100m), ("Playwright dressing", 50m));
        var page = invoice.Page;

        await Assertions.Expect(Value(page, "Client")).ToHaveTextAsync(invoice.ClientName);
        await Assertions.Expect(Value(page, "Total")).ToContainTextAsync("150.00");
        await Assertions.Expect(Value(page, "Owing")).ToContainTextAsync("150.00");
        await Assertions.Expect(page.GetByText("Playwright consult")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Playwright dressing")).ToBeVisibleAsync();
    }

    // ---- Receipts, write-off, cancel, claim status ---------------------------------------------

    [Fact]
    public async Task FullReceipt_MarksPaid_AndItsReceiptPrints()
    {
        var invoice = await CreateInvoiceAsync(("Playwright full payment", 80m));
        var page = invoice.Page;

        await RecordReceiptAsync(page);

        await Assertions.Expect(Value(page, "Status")).ToHaveTextAsync("Paid");
        await Assertions.Expect(Value(page, "Owing")).ToContainTextAsync("0.00");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "+ Record Receipt" })).ToBeDisabledAsync();

        var receiptHref = await page.GetByTitle("Print Receipt").First.GetAttributeAsync("href");
        await page.GotoAsync(receiptHref!);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "RECEIPT" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("body")).ToContainTextAsync("80.00");
    }

    [Fact]
    public async Task PartialReceipt_MarksPartiallyPaid_AndBlocksCancelling()
    {
        var invoice = await CreateInvoiceAsync(("Playwright part payment", 100m));
        var page = invoice.Page;

        await RecordReceiptAsync(page, amount: 40m);

        await Assertions.Expect(Value(page, "Status")).ToHaveTextAsync("PartiallyPaid");
        await Assertions.Expect(Value(page, "Paid")).ToContainTextAsync("40.00");
        await Assertions.Expect(Value(page, "Owing")).ToContainTextAsync("60.00");

        var cancel = page.GetByRole(AriaRole.Button, new() { Name = "Cancel Invoice" });
        await Assertions.Expect(cancel).ToBeDisabledAsync();
        await Assertions.Expect(cancel).ToHaveAttributeAsync("title", "Refund this invoice before cancelling it");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Split Invoice" })).ToBeDisabledAsync();
    }

    [Fact]
    public async Task WriteOff_ClearsTheOwingBalance()
    {
        var invoice = await CreateInvoiceAsync(("Playwright write-off", 30m));
        var page = invoice.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Write Off", Exact = true }).ClickAsync();
        var modal = page.Locator(".modal-content", new() { HasTextString = "Write Off Invoice" });
        // Clicked straight after typing the reason — regression for the confirm button only enabling
        // once the reason box lost focus.
        await modal.Locator("input[type=number]").FillAsync("30");
        await modal.Locator("textarea").FillAsync("Playwright hardship write-off");
        await modal.GetByRole(AriaRole.Button, new() { Name = "Write Off Invoice" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Invoice written off.")).ToBeVisibleAsync();
        await Assertions.Expect(Value(page, "Written Off")).ToContainTextAsync("30.00");
        await Assertions.Expect(Value(page, "Owing")).ToContainTextAsync("0.00");
    }

    [Fact]
    public async Task CancelInvoice_RequiresAReason_AndMarksCancelled()
    {
        var invoice = await CreateInvoiceAsync(("Playwright cancel me", 25m));
        var page = invoice.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel Invoice" }).ClickAsync();
        var modal = page.Locator(".modal-content", new() { HasTextString = "Keep Invoice" });
        var confirm = modal.GetByRole(AriaRole.Button, new() { Name = "Cancel Invoice" });
        await Assertions.Expect(confirm).ToBeDisabledAsync();

        await modal.Locator("textarea").FillAsync("Raised against the wrong client");
        await confirm.ClickAsync(); // straight after typing — the reason binds on input

        await Assertions.Expect(page.GetByText("Invoice cancelled.")).ToBeVisibleAsync();
        await Assertions.Expect(Value(page, "Status")).ToHaveTextAsync("Cancelled");
    }

    [Fact]
    public async Task ClaimStatus_ChangedByHand_IsSavedAndPersists()
    {
        var invoice = await CreateInvoiceAsync(("Playwright claim", 60m));
        var page = invoice.Page;

        var claimStatus = Value(page, "Claim Status").Locator("select");
        await Assertions.Expect(claimStatus).ToHaveValueAsync("NotSubmitted");
        await claimStatus.SelectOptionAsync("Submitted");
        await Assertions.Expect(page.GetByText("Claim status updated.")).ToBeVisibleAsync();

        await page.ReloadAsync();
        await Assertions.Expect(Value(page, "Claim Status").Locator("select")).ToHaveValueAsync("Submitted");
    }

    // ---- Split ---------------------------------------------------------------------------------

    [Fact]
    public async Task SplitInvoice_MovesALineToANewInvoice()
    {
        var invoice = await CreateInvoiceAsync(("Playwright stays", 70m), ("Playwright moves", 30m));
        var page = invoice.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Split Invoice" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains($"/invoices/split/{invoice.Id}"));

        var splitButton = page.GetByRole(AriaRole.Button, new() { Name = "Split Invoice" });
        await Assertions.Expect(splitButton).ToBeDisabledAsync();

        await page.Locator("tr", new() { HasTextString = "Playwright moves" }).Locator("select")
            .SelectOptionAsync(new SelectOptionValue { Value = "-1" });
        await page.Field("Reason").FillAsync("Second payer");
        await splitButton.ClickAsync(); // straight after typing — the reason binds on input

        await Assertions.Expect(page.GetByText("was split into 2 invoices.", new() { Exact = false })).ToBeVisibleAsync();

        var newInvoiceHref = await page.GetByRole(AriaRole.Link, new() { Name = "New Invoice 1" }).GetAttributeAsync("href");
        await page.GotoAsync(newInvoiceHref!);
        await Assertions.Expect(page.GetByText("Playwright moves")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Playwright stays")).ToHaveCountAsync(0);
        await Assertions.Expect(Value(page, "Total")).ToContainTextAsync("30.00");

        await page.GotoAsync($"/invoices/edit/{invoice.Id}");
        await Assertions.Expect(Value(page, "Total")).ToContainTextAsync("70.00");
    }

    // ---- Lists and print -----------------------------------------------------------------------

    [Fact]
    public async Task InvoiceSearch_FilteredByClient_ListsTheInvoice_AndOpensIt()
    {
        var invoice = await CreateInvoiceAsync(("Playwright searchable", 45m));
        var page = invoice.Page;

        await page.GotoAsync("/invoices");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Invoice Search" }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Find", Exact = true }).First.ClickAsync();
        await page.GetByPlaceholder("Search by name...").FillAsync(invoice.Surname);
        await page.Locator(".list-group-item", new() { HasTextString = invoice.Surname }).ClickAsync();
        await page.Locator("button.btn-primary", new() { HasTextString = "Find" }).ClickAsync();

        var row = page.Locator(".rz-data-row", new() { HasTextString = invoice.ClientName });
        await Assertions.Expect(row).ToHaveCountAsync(1);
        await Assertions.Expect(row).ToContainTextAsync("45.00");

        await row.Locator("a.link-primary").ClickAsync();
        await page.WaitForURLAsync(url => url.EndsWith($"/invoices/edit/{invoice.Id}"));
    }

    [Fact]
    public async Task Payments_ListsAReceipt_AndOpensItsInvoice()
    {
        var invoice = await CreateInvoiceAsync(("Playwright paid line", 55m));
        var page = invoice.Page;
        var reference = UiSeedHelpers.Unique("REF");
        await RecordReceiptAsync(page, reference: reference);

        await page.GotoAsync("/invoices/payments");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Payments" }).WaitForAsync();

        var row = page.Locator(".rz-data-row", new() { HasTextString = reference });
        await Assertions.Expect(row).ToContainTextAsync(invoice.ClientName);
        await Assertions.Expect(row).ToContainTextAsync("55.00");

        await row.Locator("a.link-primary").ClickAsync();
        await page.WaitForURLAsync(url => url.EndsWith($"/invoices/edit/{invoice.Id}"));
    }

    [Fact]
    public async Task InvoicePrint_ShowsClientLinesAndTotal()
    {
        var invoice = await CreateInvoiceAsync(("Playwright printable", 120m));
        var page = invoice.Page;

        var printHref = await page.GetByRole(AriaRole.Link, new() { Name = "Print Invoice" }).GetAttributeAsync("href");
        await page.GotoAsync(printHref!);

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "INVOICE" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("body")).ToContainTextAsync(invoice.ClientName);
        await Assertions.Expect(page.Locator("body")).ToContainTextAsync("Playwright printable");
        await Assertions.Expect(page.Locator("body")).ToContainTextAsync("120.00");
    }
}

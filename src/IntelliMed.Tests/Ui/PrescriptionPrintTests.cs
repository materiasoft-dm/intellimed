using IntelliMed.Core.Entities;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers the last leg of prescribing that PrescriptionWizardTests stops short of: issuing a created
// prescription ("Issue & Print Now") and the printable script it lands on (PrescriptionPrint.tsx).
[Collection("Playwright UI")]
public class PrescriptionPrintTests : UiTestBase
{
    public PrescriptionPrintTests(PlaywrightServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task IssueAndPrintNow_OpensAPrintableScript_WithPatientPrescriberAndMedicine()
    {
        var medicine = UiSeedHelpers.Unique("Playwright Printable ");
        await using (var context = Fixture.OpenDb())
        {
            context.Medicines.Add(new Medicine { Name = medicine, IsActive = true, Source = MedicineSource.Manual });
            await context.SaveChangesAsync();
        }
        var prescriberLastName = UiSeedHelpers.Unique("Script");
        await Fixture.SeedPractitionerAsync("Petra", prescriberLastName);

        var page = await Fixture.NewAuthenticatedPageAsync();
        var surname = UiSeedHelpers.Unique("Rx");
        var clientId = await page.CreateClientAsync(surname, "Printed");

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");
        await page.GetByPlaceholder("Search by name...").FillAsync(medicine);
        await page.Locator(".imed-medicine-results tbody tr", new() { HasTextString = medicine }).First.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        // Long-term (default) unrestricted script: Dosage → Reason → Review.
        await page.Field("Dose").FillAsync("500mg");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await Assertions.Expect(page.Locator(".card-header:has-text('6. Reason')")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await Assertions.Expect(page.Locator(".card-header:has-text('7. Review')")).ToBeVisibleAsync();

        await page.Locator(".card").Filter(new() { HasText = "Prescriber" }).Locator("select")
            .SelectOptionAsync(new SelectOptionValue { Label = $"Dr Petra {prescriberLastName}" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Prescription" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Prescription created")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Issue & Print Now" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/prescriptions/print/"));

        var body = page.Locator("body");
        await Assertions.Expect(page.GetByText("Script No:", new() { Exact = false })).ToBeVisibleAsync();
        await Assertions.Expect(body).ToContainTextAsync($"Printed {surname}");
        await Assertions.Expect(body).ToContainTextAsync($"Dr Petra {prescriberLastName}");
        await Assertions.Expect(body).ToContainTextAsync(medicine);
        await Assertions.Expect(body).ToContainTextAsync("500mg");
    }

    [Fact]
    public async Task PrintPage_ForAnUnknownIssue_ShowsNotFound()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/prescriptions/print/999999");

        await Assertions.Expect(page.GetByText("Prescription issue not found.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Script No:", new() { Exact = false })).ToHaveCountAsync(0);
    }
}

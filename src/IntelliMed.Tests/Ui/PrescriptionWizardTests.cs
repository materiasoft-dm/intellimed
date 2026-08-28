using System.Text.RegularExpressions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers PrescribeNew.razor's step wizard end to end: the step sequence and skip/gating logic
// mirror legacy Medinet's prescribing flow (see PRESCRIBING-BUSINESS-RULES.md and
// [[project_legacy_medinet_prescribing]]) — Quantity/Repeats only for Limited scripts,
// Authority/Restricted-Benefit only when the medicine's RestrictionLevel triggers it, State
// Authority only for Schedule 8 (and mandatory there).
[Collection("Playwright UI")]
public class PrescriptionWizardTests : UiTestBase
{
    public PrescriptionWizardTests(PlaywrightServerFixture fixture) : base(fixture) { }

    /// <summary>Creates a client through the real UI flow (same pattern as ClientUpdateTests) and
    /// returns the page (now on the edit route) plus its new id.</summary>
    private static async Task<(IPage Page, string Id)> CreateClientAsync(PlaywrightServerFixture fixture, string surname, string given)
    {
        var page = await fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/clients/add");
        await page.WaitForSelectorAsync("text=New Client");
        await page.Field("Surname").FillAsync(surname);
        await page.Field("Given").FillAsync(given);
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/clients/edit/"));

        var match = Regex.Match(page.Url, @"/clients/edit/(\d+)");
        return (page, match.Groups[1].Value);
    }

    /// <summary>No create screen or API exists yet for Practitioner (PractitionersController is
    /// deliberately read-only — it only backs dropdowns), so seed directly against the running
    /// server's own SQLite file, the same file it already has open, rather than driving a
    /// nonexistent screen. Each test uses a distinctively-named practitioner to avoid dropdown
    /// ambiguity with other tests sharing this fixture's database.</summary>
    private async Task<int> SeedPractitionerAsync(string firstName, string lastName)
    {
        // Pooling=false: a pooled connection would keep the file handle open past this method's
        // return, which made the fixture's own teardown fail to delete the SQLite file with a
        // "process cannot access the file" IOException.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Fixture.DbPath};Pooling=false")
            .Options;
        await using var context = new AppDbContext(options);
        var practitioner = new Practitioner { FirstName = firstName, LastName = lastName, Title = "Dr", IsActive = true };
        context.Practitioners.Add(practitioner);
        await context.SaveChangesAsync();
        return practitioner.Id;
    }

    /// <summary>Adds a medicine through the real Medicines Admin UI (same flow MedicinesAdminTests
    /// uses) with the given Schedule/Restriction so the wizard's gating logic has something to key
    /// off. Leaves the page on the medicines admin list when done.</summary>
    private static async Task AddMedicineAsync(IPage page, string name, string? schedule = null, string? restriction = null)
    {
        await page.GotoAsync("/admin/medicines");
        await page.WaitForSelectorAsync("text=Medicine Catalog");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Medicine" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Medicine");

        await page.Field("Name").FillAsync(name);
        if (schedule != null)
            await page.Field("Schedule").SelectOptionAsync(new SelectOptionValue { Value = schedule });
        if (restriction != null)
            await page.Field("Restriction").SelectOptionAsync(new SelectOptionValue { Value = restriction });

        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Medicine", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
    }

    /// <summary>Selects a real catalog match, not the "use as free text" fallback. The fallback
    /// button's text ("Can't find it? Use "name" as free text") also contains the medicine name, and
    /// it renders as soon as the search box is non-empty — before the async search results list
    /// does — so a plain GetByText(medicineName) race-clicked it instead of the actual result while
    /// the list was still loading, silently producing a free-text (no RestrictionLevel/Schedule)
    /// prescription and making every gating-step assertion downstream fail. Scoping to the result
    /// list specifically avoids that race. The search box lives inline on the wizard's own Medicine
    /// step (no picker modal), so no button click is needed to reach it.</summary>
    private static async Task SelectMedicineInWizardAsync(IPage page, string medicineName)
    {
        await page.GetByPlaceholder("Search by name...").FillAsync(medicineName);
        var result = page.Locator("ul.list-group li", new PageLocatorOptions { HasTextString = medicineName }).First;
        await result.ClickAsync();
    }

    /// <summary>Seeds a medicine carrying PBS prescribing limits. These fields only ever arrive from
    /// the PBS sync — the Add Medicine form doesn't expose them — so they have to be written
    /// directly, same approach as SeedPractitionerAsync.</summary>
    private async Task SeedPbsMedicineAsync(
        string name, int? maxRepeats = null, int? maxQuantity = null, bool isSection100 = false)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Fixture.DbPath};Pooling=false")
            .Options;
        await using var context = new AppDbContext(options);
        context.Medicines.Add(new Medicine
        {
            Name = name,
            IsActive = true,
            Source = MedicineSource.Synced,
            IsPbsListed = true,
            RestrictionLevel = PbsRestrictionLevel.Unrestricted,
            MaxRepeats = maxRepeats,
            MaxQuantity = maxQuantity,
            IsSection100 = isSection100
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Section100Medicine_RoutesThroughAuthority_EvenWhenUnrestricted()
    {
        // Legacy §3: Section 100 is an Authority trigger in its own right, independent of the
        // benefit type. This medicine is Unrestricted, so without the flag there'd be no Authority step.
        await SeedPbsMedicineAsync("Playwright Section100 Drug", isSection100: true);
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardS100", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");

        await SelectMedicineInWizardAsync(page, "Playwright Section100 Drug");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();  // Medicine
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();  // Dosage (long term)

        await Assertions.Expect(page.Locator(".card-header:has-text('Authority')")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Section 100")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ExceedingListedRepeats_RoutesThroughAuthority()
    {
        // Legacy §3's "overdose" trigger. Unrestricted medicine, so Authority can only be reached
        // by going over the listing's repeat limit.
        await SeedPbsMedicineAsync("Playwright Repeat Limit Drug", maxRepeats: 2);
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardOverdose", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");

        await SelectMedicineInWizardAsync(page, "Playwright Repeat Limit Drug");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        // Limited, so the Quantity & Repeats step is shown.
        await page.Locator("label[for=durLimited]").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        await Assertions.Expect(page.Locator(".card-header:has-text('Quantity')")).ToBeVisibleAsync();
        // Repeats pre-filled from the listing; push past it.
        await Assertions.Expect(page.Field("Repeats")).ToHaveValueAsync("2");
        await page.Field("Quantity").FillAsync("30");
        await page.Field("Duration (days)").FillAsync("30");
        await page.Field("Repeats").FillAsync("5");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        await Assertions.Expect(page.Locator(".card-header:has-text('Authority')")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("exceeds the PBS listing")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task WithinListedLimits_SkipsAuthorityEntirely()
    {
        await SeedPbsMedicineAsync("Playwright Within Limits Drug", maxRepeats: 5);
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardWithinLimits", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");

        await SelectMedicineInWizardAsync(page, "Playwright Within Limits Drug");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        // Unrestricted, not Section 100, within limits — straight to Reason.
        await Assertions.Expect(page.Locator(".card-header:has-text('Authority')")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(".card-header:has-text('Reason')")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Wizard_StartsOnMedicineStep_WithInlineSearchVisible()
    {
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardStart", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");

        await Assertions.Expect(page.Locator(".card-header:has-text('1. Medicine')")).ToBeVisibleAsync();
        // The drug search box is inline on the wizard page itself — no "Find Medicine" picker modal.
        await Assertions.Expect(page.GetByPlaceholder("Search by name...")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".modal")).ToHaveCountAsync(0);
        // Continue with nothing selected must be blocked with a validation message.
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Select or enter a medicine to continue.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task MedicineSearchResults_ShowScheduleSubsidyAndRestrictionInfo()
    {
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardSearchInfo", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await AddMedicineAsync(page, "Playwright Wizard Info Drug", schedule: "S4", restriction: "AuthorityRequired");

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");

        await page.GetByPlaceholder("Search by name...").FillAsync("Playwright Wizard Info Drug");
        var result = page.Locator("ul.list-group li", new PageLocatorOptions { HasTextString = "Playwright Wizard Info Drug" }).First;
        await Assertions.Expect(result.GetByText("S4")).ToBeVisibleAsync();
        await Assertions.Expect(result.GetByText("Authority Required")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task UnrestrictedLongTermMedicine_SkipsQuantityAuthorityAndStateAuthoritySteps()
    {
        var practitionerId = await SeedPractitionerAsync("Alice", "Wizard1");
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardLongTerm", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await AddMedicineAsync(page, "Playwright Wizard Unrestricted Drug");

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");

        await SelectMedicineInWizardAsync(page, "Playwright Wizard Unrestricted Drug");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        // Dosage step — leave duration as the default "Long term", so the next Continue must jump
        // straight past Quantity/Repeats, Authority and State Authority into Reason.
        await Assertions.Expect(page.Locator(".card-header:has-text('2. Dosage')")).ToBeVisibleAsync();
        await page.Field("Dose").FillAsync("500mg");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        await Assertions.Expect(page.Locator(".card-header:has-text('6. Reason')")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        await Assertions.Expect(page.Locator(".card-header:has-text('7. Review')")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Long term")).ToBeVisibleAsync();

        var prescriberSelect = page.Locator(".card").Filter(new LocatorFilterOptions { HasText = "Prescriber" }).Locator("select");
        await prescriberSelect.SelectOptionAsync(new SelectOptionValue { Label = "Dr Alice Wizard1" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Prescription" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Prescription created")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AuthorityRequiredLimitedMedicine_ShowsQuantityAndAuthoritySteps_ValidatesRequiredFields()
    {
        await SeedPractitionerAsync("Bob", "Wizard2");
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardAuthority", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await AddMedicineAsync(page, "Playwright Wizard Authority Drug", restriction: "AuthorityRequired");

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");

        await SelectMedicineInWizardAsync(page, "Playwright Wizard Authority Drug");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        await page.Field("Dose").FillAsync("10mg");
        // Switch to Limited duration so the Quantity/Repeats step is not skipped.
        await page.Locator("label[for=durLimited]").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        await Assertions.Expect(page.Locator(".card-header:has-text('3. Quantity')")).ToBeVisibleAsync();
        // Blocked without quantity/duration — mirrors legacy §5's "quantity must be non-empty /
        // duration must be > 0 for Limited" validation.
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Quantity is required for a limited-duration script.")).ToBeVisibleAsync();

        await page.Field("Quantity").FillAsync("30");
        await page.Field("Duration (days)").FillAsync("10");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        await Assertions.Expect(page.Locator(".card-header:has-text('4. Authority')")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Authority Required")).ToBeVisibleAsync();
        await page.Field("Authority approval number").FillAsync("A123456");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        // Schedule 8 step must NOT appear for a non-S8 medicine.
        await Assertions.Expect(page.Locator(".card-header:has-text('State Authority')")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(".card-header:has-text('6. Reason')")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RestrictedBenefitMedicine_RequiresAcknowledgementCheckboxBeforeContinuing()
    {
        await SeedPractitionerAsync("Carol", "Wizard3");
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardRestricted", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await AddMedicineAsync(page, "Playwright Wizard RB Drug", restriction: "Restricted");

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");

        await SelectMedicineInWizardAsync(page, "Playwright Wizard RB Drug");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync(); // Dosage, defaults to Long term

        await Assertions.Expect(page.Locator(".card-header:has-text('Restricted Benefit')")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("No separate")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Confirm the Restricted Benefit indication before continuing.")).ToBeVisibleAsync();

        await page.GetByLabel("Indication confirmed").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        await Assertions.Expect(page.Locator(".card-header:has-text('6. Reason')")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ScheduleEightMedicine_BlocksContinueUntilStateAuthorityCodeEntered()
    {
        await SeedPractitionerAsync("Dave", "Wizard4");
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardS8", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await AddMedicineAsync(page, "Playwright Wizard S8 Drug", schedule: "S8");

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");

        await SelectMedicineInWizardAsync(page, "Playwright Wizard S8 Drug");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync(); // Dosage, Long term

        await Assertions.Expect(page.Locator(".card-header:has-text('State Authority')")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Schedule 8 (drug of dependence)")).ToBeVisibleAsync();

        // Mandatory gate — mirrors legacy §4's "MANDATORY... the add is aborted" behaviour.
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await Assertions.Expect(page.GetByText("A state authority code is mandatory for Schedule 8 prescribing.")).ToBeVisibleAsync();

        await page.Field("State authority code").FillAsync("VIC-S8-001");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        await Assertions.Expect(page.Locator(".card-header:has-text('6. Reason')")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ArtgBackfilledMedicine_ReachesTheScheduleEightGate()
    {
        // The whole point of the ARTG backfill. PBS publishes no poisons scheduling, so every synced
        // medicine arrived with Schedule == null and the S8 gate above could never fire on real
        // catalogue data — only on hand-entered medicines. This drives the actual admin upload and
        // then checks the wizard's behaviour changed as a result, which no unit test can show.
        await SeedPractitionerAsync("Fiona", "Wizard11");
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardArtg", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        // A synced medicine in the state the PBS sync leaves them in: no Schedule at all. Its
        // leading token is deliberately unlike the other medicines in this file: ArtgEnrichmentService
        // keys on brand, i.e. the first token, so naming it "Playwright ..." like the rest would make
        // this test's ARTG row match all of them and stamp S8 across their assertions.
        await SeedPbsMedicineAsync("ArtgBackfillOpioid Playwright");

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");
        await SelectMedicineInWizardAsync(page, "ArtgBackfillOpioid Playwright");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync(); // Dosage, long term

        // Before the backfill the S8 step doesn't exist, so the wizard lands straight on Reason.
        await Assertions.Expect(page.Locator(".card-header:has-text('State Authority')")).Not.ToBeVisibleAsync();

        var csv = Path.Combine(Path.GetTempPath(), $"artg-{Guid.NewGuid():N}.csv");
        await File.WriteAllLinesAsync(csv, new[]
        {
            "Applied filters: Indication is All Medicines",
            "",
            "ARTG ID,Product Name,Sponsor Name,Poison Schedule,Dosage Form,Route of Administration",
            "999001,ArtgBackfillOpioid Playwright oxycodone 5 mg tablet,Test Sponsor,(S8) Controlled Drug,Tablet,Oral"
        });

        try
        {
            await page.GotoAsync("/admin/medicines");
            await page.WaitForSelectorAsync("text=Medicine Catalog");
            await page.Locator("#artgEnrichFile").SetInputFilesAsync(csv);

            // The button is disabled until _artgFile is set, and that only happens once Blazor has
            // processed InputFile's OnChange — which is asynchronous and does not complete with
            // SetInputFilesAsync. Clicking straight away races it: the click lands on a disabled
            // button, does nothing, and the result assertion below then times out. Waiting for the
            // enabled state is the actual synchronisation point. (SlowMo's 250ms was hiding this;
            // it surfaced as a flake the moment the suite ran without it.)
            var backfill = page.GetByRole(AriaRole.Button, new() { Name = "Backfill Scheduling" });
            await Assertions.Expect(backfill).ToBeEnabledAsync();
            await backfill.ClickAsync();

            await Assertions.Expect(page.GetByText("filled 1 schedule(s)")).ToBeVisibleAsync();

            // Same medicine, same wizard, same steps — the gate now fires.
            await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
            await page.WaitForSelectorAsync("text=New Prescription");
            await SelectMedicineInWizardAsync(page, "ArtgBackfillOpioid Playwright");
            await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

            await Assertions.Expect(page.Locator(".card-header:has-text('State Authority')")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Schedule 8 (drug of dependence)")).ToBeVisibleAsync();
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Fact]
    public async Task BackButton_ReturnsToPreviousStepWithDataRetained()
    {
        await SeedPractitionerAsync("Erin", "Wizard5");
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardBack", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await AddMedicineAsync(page, "Playwright Wizard Back Drug");

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");

        await SelectMedicineInWizardAsync(page, "Playwright Wizard Back Drug");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        await page.Field("Dose").FillAsync("250mg");
        await page.GetByRole(AriaRole.Button, new() { Name = "Back" }).ClickAsync();

        await Assertions.Expect(page.Locator(".card-header:has-text('1. Medicine')")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Playwright Wizard Back Drug")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await Assertions.Expect(page.Field("Dose")).ToHaveValueAsync("250mg");
    }

    [Fact]
    public async Task ReasonStep_CanCreateNewEpisodeInline()
    {
        await SeedPractitionerAsync("Frank", "Wizard6");
        var (_, clientId) = await CreateClientAsync(Fixture, "WizardEpisode", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await AddMedicineAsync(page, "Playwright Wizard Episode Drug");

        await page.GotoAsync($"/clients/{clientId}/prescriptions/new");
        await page.WaitForSelectorAsync("text=New Prescription");

        await SelectMedicineInWizardAsync(page, "Playwright Wizard Episode Drug");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync(); // Dosage

        await Assertions.Expect(page.Locator(".card-header:has-text('6. Reason')")).ToBeVisibleAsync();
        await page.Field("Reason for prescribing").FillAsync("Acute sinusitis");

        await page.GetByRole(AriaRole.Button, new() { Name = "+ New" }).ClickAsync();
        await page.GetByPlaceholder("New episode title").FillAsync("Sinusitis — recurring");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add" }).ClickAsync();

        // Episode dropdown reappears with the new episode selected.
        await Assertions.Expect(page.GetByPlaceholder("New episode title")).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        await Assertions.Expect(page.Locator(".card-header:has-text('7. Review')")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Acute sinusitis")).ToBeVisibleAsync();
    }
}

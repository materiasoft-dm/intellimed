using System.Text.RegularExpressions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers ProgressNoteEditor.razor: the draft -> finalize -> addendum lifecycle, the walk-in
// auto-appointment behaviour documented in PATIENT_VISIT_WORKFLOW.md, and the append-only rule that
// a finalized note can never be edited in place.
[Collection("Playwright UI")]
public class ProgressNoteTests : UiTestBase
{
    public ProgressNoteTests(PlaywrightServerFixture fixture) : base(fixture) { }

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

    private AppDbContext OpenFixtureDb()
    {
        // Pooling=false so the file handle is released when the context is disposed — otherwise the
        // fixture's own teardown can't delete the SQLite file.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Fixture.DbPath};Pooling=false")
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>See PrescriptionWizardTests.SeedPractitionerAsync — Practitioner has no create UI or
    /// API, so it's seeded straight into the fixture's own SQLite file.</summary>
    private async Task<int> SeedPractitionerAsync(string firstName, string lastName)
    {
        await using var context = OpenFixtureDb();
        var practitioner = new Practitioner { FirstName = firstName, LastName = lastName, Title = "Dr", IsActive = true };
        context.Practitioners.Add(practitioner);
        await context.SaveChangesAsync();
        return practitioner.Id;
    }

    private static async Task SelectPractitionerAsync(IPage page, string label)
    {
        await page.Field("Practitioner").SelectOptionAsync(new SelectOptionValue { Label = label });
    }

    /// <summary>
    /// Gives the signed-in admin access to a clinic and makes it the active one.
    ///
    /// Two separate things are needed, and both are easy to miss. A fresh test database has no
    /// UserClinic rows at all (nothing seeds them), and MainLayout clears any stored clinic id the
    /// user has no membership for — so setting local storage alone is silently undone on the next
    /// render. The membership row has no create UI, hence the direct DB write, same approach as
    /// SeedPractitionerAsync.
    ///
    /// This matters because ResolveAppointmentIdForCreateAsync returns null when there is no current
    /// clinic, skipping walk-in appointment creation. In the real app that state is unreachable —
    /// MainLayout auto-selects for single-clinic users and shows a blocking "Select a Clinic" modal
    /// for multi-clinic ones — but a fresh test database has no memberships at all, which is how the
    /// tests would otherwise land in it.
    /// </summary>
    private async Task SelectClinicAsync(IPage page, int clinicId = 1)
    {
        await using (var context = OpenFixtureDb())
        {
            var adminId = await context.Users
                .Where(u => u.Email == "admin@clinic.com")
                .Select(u => u.Id)
                .FirstAsync();

            if (!await context.UserClinics.AnyAsync(uc => uc.ApplicationUserId == adminId && uc.ClinicId == clinicId))
            {
                context.UserClinics.Add(new UserClinic { ApplicationUserId = adminId, ClinicId = clinicId });
                await context.SaveChangesAsync();
            }
        }

        await page.EvaluateAsync(
            "id => localStorage.setItem('intellimed_current_clinic_id', id)", clinicId.ToString());
        await page.ReloadAsync();
    }

    [Fact]
    public async Task NewProgressNote_Loads_WithSoapFieldsAndBothSaveActions()
    {
        var (_, clientId) = await CreateClientAsync(Fixture, "NoteLoad", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await page.GotoAsync($"/clients/{clientId}/progress-notes/new");
        await page.WaitForSelectorAsync("text=Reason for Visit");

        foreach (var field in new[] { "Subjective", "Objective", "Assessment", "Plan" })
        {
            await Assertions.Expect(page.Field(field)).ToBeVisibleAsync();
        }

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save Draft" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Finalize & Sign" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SaveDraft_PersistsSoapContentAndStaysEditable()
    {
        await SeedPractitionerAsync("Nora", "Notes1");
        var (_, clientId) = await CreateClientAsync(Fixture, "NoteDraft", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await page.GotoAsync($"/clients/{clientId}/progress-notes/new");
        await page.WaitForSelectorAsync("text=Reason for Visit");

        await page.Field("Reason for Visit").FillAsync("Headache");
        await page.Field("Subjective").FillAsync("Throbbing, 3 days");
        await page.Field("Assessment").FillAsync("Tension headache");
        await SelectPractitionerAsync(page, "Dr Nora Notes1");

        await page.GetByRole(AriaRole.Button, new() { Name = "Save Draft" }).ClickAsync();
        await page.WaitForURLAsync(url => Regex.IsMatch(url, @"/progress-notes/\d+"));

        await page.ReloadAsync();
        await page.WaitForSelectorAsync("text=Reason for Visit");

        await Assertions.Expect(page.Field("Reason for Visit")).ToHaveValueAsync("Headache");
        await Assertions.Expect(page.Field("Subjective")).ToHaveValueAsync("Throbbing, 3 days");
        // Still a draft, so both actions remain available.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Finalize & Sign" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task FirstSave_AutoCreatesAWalkInAppointment_AndLinksTheNoteToIt()
    {
        // PATIENT_VISIT_WORKFLOW.md: a note started from the client record with no prior booking
        // creates its own same-day appointment on first save, so the visit is a real, reportable
        // encounter rather than a floating note.
        await SeedPractitionerAsync("Walt", "Notes2");
        var (_, clientId) = await CreateClientAsync(Fixture, "NoteWalkIn", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await page.GotoAsync($"/clients/{clientId}/progress-notes/new");
        await page.WaitForSelectorAsync("text=Reason for Visit");
        await SelectClinicAsync(page);
        await page.WaitForSelectorAsync("text=Reason for Visit");

        await page.Field("Reason for Visit").FillAsync("Walk-in sore throat");
        await SelectPractitionerAsync(page, "Dr Walt Notes2");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save Draft" }).ClickAsync();
        await page.WaitForURLAsync(url => Regex.IsMatch(url, @"/progress-notes/\d+"));

        // Asserted against the database rather than /api/appointments/search, which server-side
        // overwrites ClinicId with the caller's current clinic — that scoping is a separate concern
        // from "did the first save create and link an appointment at all", which is what's under test.
        await using var context = OpenFixtureDb();
        var id = int.Parse(clientId);

        var appointment = await context.Appointments
            .Where(a => a.ClientId == id)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();

        Assert.NotNull(appointment);
        Assert.Equal("Auto-created for a walk-in progress note", appointment!.Notes);
        Assert.Equal(AppointmentStatus.InProgress, appointment.Status);

        var note = await context.ProgressNotes
            .Where(n => n.ClientId == id)
            .OrderByDescending(n => n.Id)
            .FirstAsync();
        Assert.Equal(appointment.Id, note.AppointmentId);
    }

    [Fact]
    public async Task FinalizeAndSign_LocksTheNote_AndOffersAddendumInstead()
    {
        await SeedPractitionerAsync("Fiona", "Notes3");
        var (_, clientId) = await CreateClientAsync(Fixture, "NoteFinal", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await page.GotoAsync($"/clients/{clientId}/progress-notes/new");
        await page.WaitForSelectorAsync("text=Reason for Visit");
        await page.Field("Reason for Visit").FillAsync("Ankle sprain");
        await page.Field("Plan").FillAsync("RICE, review in 1 week");
        await SelectPractitionerAsync(page, "Dr Fiona Notes3");

        await page.GetByRole(AriaRole.Button, new() { Name = "Finalize & Sign" }).ClickAsync();
        await page.WaitForSelectorAsync("text=Finalized");

        // The signed record is append-only: the SOAP editor is gone, replaced by an addendum box.
        await Assertions.Expect(page.GetByText("Finalized")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Finalize & Sign" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.Field("Add addendum")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Addendum_OnAFinalizedNote_IsAppendedAndDisplayed()
    {
        await SeedPractitionerAsync("Adam", "Notes4");
        var (_, clientId) = await CreateClientAsync(Fixture, "NoteAddendum", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await page.GotoAsync($"/clients/{clientId}/progress-notes/new");
        await page.WaitForSelectorAsync("text=Reason for Visit");
        await page.Field("Reason for Visit").FillAsync("Follow-up");
        await SelectPractitionerAsync(page, "Dr Adam Notes4");
        await page.GetByRole(AriaRole.Button, new() { Name = "Finalize & Sign" }).ClickAsync();
        await page.WaitForSelectorAsync("text=Finalized");

        // The addendum textarea uses a plain @bind (change-on-blur), and the Add button stays
        // disabled until that binding actually flushes — so tab out rather than clicking straight
        // from the still-focused field.
        await page.Field("Add addendum").FillAsync("Pathology result received: CRP normal.");
        await page.Field("Add addendum").PressAsync("Tab");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Addendum" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Pathology result received: CRP normal.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Episode_CanBeCreatedInline_AndTagsTheNote()
    {
        await SeedPractitionerAsync("Ellie", "Notes5");
        var (_, clientId) = await CreateClientAsync(Fixture, "NoteEpisode", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await page.GotoAsync($"/clients/{clientId}/progress-notes/new");
        await page.WaitForSelectorAsync("text=Reason for Visit");

        await page.GetByRole(AriaRole.Button, new() { Name = "+ New" }).ClickAsync();
        await page.GetByPlaceholder("New episode title").FillAsync("Right knee injury");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add" }).ClickAsync();

        await Assertions.Expect(page.GetByPlaceholder("New episode title")).ToHaveCountAsync(0);

        await page.Field("Reason for Visit").FillAsync("Knee review");
        await SelectPractitionerAsync(page, "Dr Ellie Notes5");
        await page.GetByRole(AriaRole.Button, new() { Name = "Finalize & Sign" }).ClickAsync();
        await page.WaitForSelectorAsync("text=Finalized");

        await Assertions.Expect(page.GetByText("Right knee injury")).ToBeVisibleAsync();
    }
}

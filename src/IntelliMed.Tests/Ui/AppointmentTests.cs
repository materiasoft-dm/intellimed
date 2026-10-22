using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers booking (NewAppointment.razor), the appointment details panel and its visit-status
// workflow (AppointmentEdit.razor / AppointmentDetailsPanel.razor), the list, the waiting room, the
// calendar and the printable day list. Each test seeds its own practitioner so bookings made by
// different tests on the shared database never overlap.
[Collection("Playwright UI")]
public class AppointmentTests : UiTestBase
{
    public AppointmentTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private record Booking(IPage Page, int AppointmentId, int ClientId, string ClientName, int PractitionerId);

    /// <summary>Books a 09:00 appointment for today through the New Appointment screen, using the
    /// client picker and practitioner dropdown, and returns once the details page has loaded.</summary>
    private async Task<Booking> BookTodayAsync(string notes = "")
    {
        var surname = UiSeedHelpers.Unique("Appt");
        var practitionerId = await Fixture.SeedPractitionerAsync("Booking", UiSeedHelpers.Unique("Prac"));

        var page = await Fixture.NewAuthenticatedPageAsync();
        var clientId = await page.CreateClientAsync(surname, "Booked");

        await page.GotoAsync("/appointments/new");
        await page.GetByRole(AriaRole.Heading, new() { Name = "New Appointment" }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Find", Exact = true }).ClickAsync();
        await page.GetByPlaceholder("Search by name...").FillAsync(surname);
        await page.Locator(".list-group-item", new() { HasTextString = surname }).ClickAsync();
        await Assertions.Expect(page.GetByPlaceholder("Search for a client...")).ToHaveValueAsync($"Booked {surname}");

        await page.Field("Practitioner").SelectOptionAsync(new SelectOptionValue { Value = practitionerId.ToString() });
        if (notes.Length > 0) await page.Field("Notes").FillAsync(notes);

        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/appointments/edit/"));
        await page.GetByRole(AriaRole.Heading, new() { Name = "Appointment Details" }).WaitForAsync();

        return new Booking(page, UiSeedHelpers.IdFromUrl(page.Url), clientId, $"Booked {surname}", practitionerId);
    }

    private static ILocator StatusBadge(IPage page) =>
        page.Locator("xpath=//label[normalize-space(text())='Status']/following-sibling::div//span[contains(@class,'badge')]");

    // ---- Booking & details ---------------------------------------------------------------------

    [Fact]
    public async Task NewAppointment_BookedThroughPickerAndPractitioner_OpensItsDetailsAsScheduled()
    {
        var booking = await BookTodayAsync(notes: "Playwright booking note");

        await Assertions.Expect(StatusBadge(booking.Page)).ToHaveTextAsync("Scheduled");
        await Assertions.Expect(booking.Page.Locator("main")).ToContainTextAsync(booking.ClientName);
        await Assertions.Expect(booking.Page.Field("Notes")).ToHaveValueAsync("Playwright booking note");
    }

    [Fact]
    public async Task NewAppointment_QueryString_PrefillsClientAndPractitioner()
    {
        var surname = UiSeedHelpers.Unique("Prefill");
        var practitionerId = await Fixture.SeedPractitionerAsync("Prefill", UiSeedHelpers.Unique("Prac"));
        var page = await Fixture.NewAuthenticatedPageAsync();
        var clientId = await page.CreateClientAsync(surname, "Query");

        await page.GotoAsync($"/appointments/new?clientId={clientId}&practitionerId={practitionerId}");

        await Assertions.Expect(page.GetByPlaceholder("Search for a client...")).ToHaveValueAsync($"Query {surname}");
        await Assertions.Expect(page.Field("Practitioner")).ToHaveValueAsync(practitionerId.ToString());
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeEnabledAsync();
    }

    [Fact]
    public async Task NewAppointment_SaveIsDisabled_UntilClientAndPractitionerAreChosen()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/appointments/new");
        await page.GetByRole(AriaRole.Heading, new() { Name = "New Appointment" }).WaitForAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Details_EditingNotes_SavesAndPersists()
    {
        var booking = await BookTodayAsync();
        var page = booking.Page;

        await page.Field("Notes").FillAsync("Changed by Playwright");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Saved.", new() { Exact = true })).ToBeVisibleAsync();

        await page.ReloadAsync();
        await Assertions.Expect(page.Field("Notes")).ToHaveValueAsync("Changed by Playwright");
    }

    [Fact]
    public async Task Details_VisitWorkflow_ArrivedStartComplete_ThenStartInvoice()
    {
        var booking = await BookTodayAsync();
        var page = booking.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Mark Arrived" }).ClickAsync();
        await Assertions.Expect(StatusBadge(page)).ToHaveTextAsync("Arrived");

        await page.GetByRole(AriaRole.Button, new() { Name = "Start", Exact = true }).ClickAsync();
        await Assertions.Expect(StatusBadge(page)).ToHaveTextAsync("InProgress");

        await page.GetByRole(AriaRole.Button, new() { Name = "Complete", Exact = true }).ClickAsync();
        await Assertions.Expect(StatusBadge(page)).ToHaveTextAsync("Completed");

        await page.GetByRole(AriaRole.Button, new() { Name = "Start Invoice" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/invoices/new")
            && url.Contains($"clientId={booking.ClientId}")
            && url.Contains($"appointmentId={booking.AppointmentId}"));
    }

    [Fact]
    public async Task Details_Cancel_MarksCancelled_AndDisablesFurtherStatusChanges()
    {
        var booking = await BookTodayAsync();
        var page = booking.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Assertions.Expect(StatusBadge(page)).ToHaveTextAsync("Cancelled");

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Mark Arrived" })).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "No-show" })).ToBeDisabledAsync();
    }

    // ---- Waiting room --------------------------------------------------------------------------

    [Fact]
    public async Task WaitingRoom_ShowsArrivedClient_AndStartThenCompleteMovesThemOn()
    {
        var booking = await BookTodayAsync();
        await booking.Page.GetByRole(AriaRole.Button, new() { Name = "Mark Arrived" }).ClickAsync();
        await Assertions.Expect(StatusBadge(booking.Page)).ToHaveTextAsync("Arrived");

        var page = booking.Page;
        await page.GotoAsync("/appointments/waiting-room");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Waiting Room" }).WaitForAsync();

        var row = page.Locator("tr", new() { HasTextString = booking.ClientName });
        await Assertions.Expect(row).ToContainTextAsync("Arrived");

        await row.GetByRole(AriaRole.Button, new() { Name = "Start", Exact = true }).ClickAsync();
        await Assertions.Expect(row).ToContainTextAsync("InProgress");

        await row.GetByRole(AriaRole.Button, new() { Name = "Complete", Exact = true }).ClickAsync();
        await Assertions.Expect(row).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task WaitingRoom_ProgressNoteButton_OpensANoteLinkedToTheAppointment()
    {
        var booking = await BookTodayAsync();
        await booking.Page.GetByRole(AriaRole.Button, new() { Name = "Mark Arrived" }).ClickAsync();
        await Assertions.Expect(StatusBadge(booking.Page)).ToHaveTextAsync("Arrived");

        var page = booking.Page;
        await page.GotoAsync("/appointments/waiting-room");
        await page.Locator("tr", new() { HasTextString = booking.ClientName })
            .GetByRole(AriaRole.Button, new() { Name = "Progress Note" }).ClickAsync();

        await page.WaitForURLAsync(url => url.Contains($"/clients/{booking.ClientId}/progress-notes/new")
            && url.Contains($"appointmentId={booking.AppointmentId}"));
    }

    // ---- List, calendar, day list --------------------------------------------------------------

    [Fact]
    public async Task AppointmentList_FilteredByClient_ShowsTheirBooking_AndOpensItsDetails()
    {
        var booking = await BookTodayAsync();
        var page = booking.Page;
        var surname = booking.ClientName.Split(' ')[1];

        await page.GotoAsync("/appointments");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Appointments", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Find", Exact = true }).First.ClickAsync();
        await page.GetByPlaceholder("Search by name...").FillAsync(surname);
        await page.Locator(".list-group-item", new() { HasTextString = surname }).ClickAsync();
        await page.Locator("button.btn-primary", new() { HasTextString = "Find" }).ClickAsync();

        var row = page.Locator(".rz-data-row", new() { HasTextString = booking.ClientName });
        await Assertions.Expect(row).ToHaveCountAsync(1);
        await Assertions.Expect(row).ToContainTextAsync("Scheduled");

        await row.Locator("a.link-primary").ClickAsync();
        await Assertions.Expect(page.Locator("main")).ToContainTextAsync(booking.ClientName);
    }

    [Fact]
    public async Task AppointmentList_ToolbarButtons_NavigateToCalendarAndNewBooking()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/appointments");

        await page.GetByRole(AriaRole.Button, new() { Name = "Calendar View" }).ClickAsync();
        await page.WaitForURLAsync(url => url.EndsWith("/appointments/calendar"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Appointment Calendar" })).ToBeVisibleAsync();

        await page.GotoAsync("/appointments");
        await page.GetByRole(AriaRole.Button, new() { Name = "New Appointment" }).ClickAsync();
        await page.WaitForURLAsync(url => url.EndsWith("/appointments/new"));
    }

    [Fact]
    public async Task RepeatBooking_CreatesTheRequestedNumberOfOccurrences()
    {
        var surname = UiSeedHelpers.Unique("Series");
        var practitionerId = await Fixture.SeedPractitionerAsync("Series", UiSeedHelpers.Unique("Prac"));
        var page = await Fixture.NewAuthenticatedPageAsync();
        var clientId = await page.CreateClientAsync(surname, "Repeat");

        await page.GotoAsync($"/appointments/new?clientId={clientId}&practitionerId={practitionerId}");
        await Assertions.Expect(page.GetByPlaceholder("Search for a client...")).ToHaveValueAsync($"Repeat {surname}");
        await page.GetByLabel("Repeat this appointment").CheckAsync();
        await page.Field("Ends after (occurrences)").FillAsync("3");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(url => url.EndsWith("/appointments"));

        await page.GetByRole(AriaRole.Button, new() { Name = "Find", Exact = true }).First.ClickAsync();
        await page.GetByPlaceholder("Search by name...").FillAsync(surname);
        await page.Locator(".list-group-item", new() { HasTextString = surname }).ClickAsync();
        await page.Locator("button.btn-primary", new() { HasTextString = "Find" }).ClickAsync();

        await Assertions.Expect(page.Locator(".rz-data-row", new() { HasTextString = $"Repeat {surname}" })).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task Calendar_ShowsTodaysBooking_AndDayListPrintsIt()
    {
        var booking = await BookTodayAsync();
        var page = booking.Page;

        await page.GotoAsync("/appointments/calendar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Appointment Calendar" }).WaitForAsync();
        // The calendar loads profile, clinic settings, practitioners and the week's appointments before
        // the scheduler renders, which can run past the default 5s on a cold page.
        await Assertions.Expect(page.Locator($"[data-appt-id='{booking.AppointmentId}']")).ToBeVisibleAsync(new() { Timeout = 15000 });

        await page.GetByRole(AriaRole.Button, new() { Name = "Day List" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/appointments/print?date="));
    }

    [Fact]
    public async Task Calendar_ShowsBookings_EvenWhenAppointmentsLoadBeforePractitioners()
    {
        // Regression (fixed 2026-10-22): AppointmentCalendar.razor filters the week's appointments by the
        // selected practitioners in RebuildEvents(). If the appointment search returned before
        // /api/practitioners/active, that filter ran against an empty set and nothing rebuilt the events
        // once the practitioners arrived — the calendar stayed empty. On a warm server the search
        // routinely wins. This test forces that ordering deterministically.
        var booking = await BookTodayAsync();
        var page = booking.Page;

        var searchReturned = new TaskCompletionSource();
        page.Response += (_, r) =>
        {
            if (r.Url.Contains("/api/appointments/search")) searchReturned.TrySetResult();
        };
        await page.RouteAsync("**/api/practitioners/active", async route =>
        {
            await Task.WhenAny(searchReturned.Task, Task.Delay(10000));
            await route.ContinueAsync();
        });

        await page.GotoAsync("/appointments/calendar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Appointment Calendar" }).WaitForAsync();
        await Assertions.Expect(page.Locator(".imed-practitioner-bar input")).Not.ToHaveCountAsync(0, new() { Timeout = 15000 });

        await Assertions.Expect(page.Locator($"[data-appt-id='{booking.AppointmentId}']")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task DayList_ShowsTodaysBooking_AndFiltersByPractitioner()
    {
        var booking = await BookTodayAsync();
        var otherPractitionerId = await Fixture.SeedPractitionerAsync("Other", UiSeedHelpers.Unique("Prac"));
        var page = booking.Page;

        await page.GotoAsync($"/appointments/print?date={DateTime.Today:yyyy-MM-dd}");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { NameRegex = new System.Text.RegularExpressions.Regex("^Day List") })).ToBeVisibleAsync();
        var row = page.Locator("tr", new() { HasTextString = booking.ClientName });
        await Assertions.Expect(row).ToBeVisibleAsync();

        await page.Locator("select").SelectOptionAsync(new SelectOptionValue { Value = otherPractitionerId.ToString() });
        await Assertions.Expect(row).ToHaveCountAsync(0);

        await page.Locator("select").SelectOptionAsync(new SelectOptionValue { Value = booking.PractitionerId.ToString() });
        await Assertions.Expect(row).ToBeVisibleAsync();
    }
}

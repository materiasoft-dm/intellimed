namespace IntelliMed.Api.Authorization;

/// <summary>
/// Sets of page keys, any one of which grants an endpoint that several features share. Pass one to
/// <see cref="RequirePageAttribute"/>. Kept here so the same list is not retyped on every action — and
/// so adding a feature that needs, say, patient lookup means changing one line.
/// </summary>
public static class PageAccess
{
    /// <summary>Looking a client up (search, open the record, read its sub-records). Anything that picks
    /// or shows a patient needs this, so every feature that does is listed — otherwise a role holding
    /// only, say, <c>appointments/create</c> could book nobody.</summary>
    public const string ClientLookup =
        "clients|appointments|appointments/create|appointments/edit|appointments/waiting-room" +
        "|invoices|invoices/create|results|prescriptions|progress-notes|health-issues";

    /// <summary>Reading an appointment, the calendar or a practitioner's schedule.</summary>
    public const string AppointmentView =
        "appointments|appointments/create|appointments/edit|appointments/waiting-room|clients|progress-notes|prescriptions";

    /// <summary>Moving an appointment through arrived / in progress / completed — the waiting room, the
    /// calendar and writing a progress note all do it.</summary>
    public const string AppointmentProgress =
        "appointments/edit|appointments/waiting-room|progress-notes/create|progress-notes/edit";

    /// <summary>Booking an appointment. Writing a note for a walk-in books a same-day one on the fly.</summary>
    public const string AppointmentCreate = "appointments/create|progress-notes/create";

    /// <summary>Reading an invoice or one of its receipts.</summary>
    public const string InvoiceView =
        "invoices|invoices/create|invoices/edit|payments|invoices/refund|invoices/write-off|invoices/void|invoices/split";

    /// <summary>Reading fee schedules. Raising an invoice prices its lines from them, so invoicing roles
    /// need to read what only a fee-schedule manager may change.</summary>
    public const string FeeScheduleView = "fee-schedules|invoices/create|invoices/edit|clinic-settings";
}

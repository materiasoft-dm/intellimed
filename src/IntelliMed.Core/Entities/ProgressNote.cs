namespace IntelliMed.Core.Entities;

/// <summary>The clinical documentation of one encounter. Subjective/Objective/Assessment/Plan/
/// ReasonForVisit are only editable while Status == Draft — they lock once Finalized (see
/// ProgressNoteRepository.UpdateAsync), matching how Prescription locks its own fields once an Issue
/// exists. Correcting a Finalized note happens only via a new ProgressNoteAddendum, never an in-place
/// edit — same medico-legal discipline as Prescription/PrescriptionIssue. No delete route once
/// Finalized; a Draft that was started by mistake can be deleted outright (see
/// ProgressNoteRepository.DeleteAsync).</summary>
public class ProgressNote
{
    public int Id { get; set; }
    public int ClientId { get; set; }

    /// <summary>Nullable — the linked "encounter." IntelliMed deliberately has no separate Encounter
    /// entity; Appointment (Status/ArrivedAt/SeenAt/CompletedAt) already models the encounter lifecycle,
    /// so this points there directly, same convention as Invoice.AppointmentId/Prescription.AppointmentId.
    /// Null for a walk-in/phone consult where the UI creates a same-day Appointment on the fly instead of
    /// inventing a second "unscheduled encounter" concept.</summary>
    public int? AppointmentId { get; set; }

    public int PractitionerId { get; set; }

    /// <summary>Optional grouping under an ongoing clinical issue — most single-visit notes leave this
    /// null.</summary>
    public int? EpisodeId { get; set; }

    public DateTime EncounterDate { get; set; }

    /// <summary>One-liner; doubles as the notes-timeline summary column.</summary>
    public string? ReasonForVisit { get; set; }

    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string? Assessment { get; set; }
    public string? Plan { get; set; }

    /// <summary>Recall/follow-up date, if any.</summary>
    public DateTime? ReviewDate { get; set; }

    public ProgressNoteStatus Status { get; set; } = ProgressNoteStatus.Draft;
    public DateTime? FinalizedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public Appointment? Appointment { get; set; }
    public Practitioner? Practitioner { get; set; }
    public Episode? Episode { get; set; }
    public ICollection<ProgressNoteAddendum> Addenda { get; set; } = new List<ProgressNoteAddendum>();
}

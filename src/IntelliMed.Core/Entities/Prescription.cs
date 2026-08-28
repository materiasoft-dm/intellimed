namespace IntelliMed.Core.Entities;

/// <summary>The current/ongoing medication order. Dose/Frequency/Route/Quantity/DurationDays/Repeats
/// are only editable while no PrescriptionIssue exists yet — they lock once the first issue is created
/// (see PrescriptionRepository.UpdateAsync), since a printed/issued script is a fixed clinical event.
/// PrescribingNotes/NotesForPharmacy stay editable regardless. Changing the underlying therapy means
/// Cease this row and create a new one, not edit in place. No IsArchived — Status already fully
/// captures the lifecycle (Ceased/Cancelled), a second overlapping flag would just be two sources of
/// truth for the same thing.</summary>
public class Prescription
{
    public int Id { get; set; }
    public int ClientId { get; set; }

    /// <summary>Nullable — a compounded/free-text item is still prescribable with no catalog match,
    /// same as Medinet's Recipe concept. MedicineName is always populated regardless so free-text
    /// entries still display/search. A null MedicineId means IMedicationInteractionChecker has no
    /// ingredients to check against — surfaced explicitly in the wizard, not silently skipped.</summary>
    public int? MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;

    public string? Dose { get; set; }
    public string? Frequency { get; set; }

    /// <summary>Patient-facing dosing instruction (e.g. "Take with food") — distinct from
    /// PrescribingNotes (clinician-facing) and NotesForPharmacy (dispenser-facing). Mirrors legacy
    /// Medinet's DoseCtrl "Instruction" field.</summary>
    public string? Instruction { get; set; }
    public string? Route { get; set; }
    public PrescriptionSourceCode? Purpose { get; set; }

    /// <summary>Mirrors legacy Medinet's CurrentRx.Limited flag. Long-term (false) scripts skip the
    /// wizard's Quantity/Repeats step entirely — DurationDays/Quantity/Repeats stay whatever the
    /// prescriber last set (usually unset) rather than being required.</summary>
    public bool IsLimitedDuration { get; set; }

    public string? Quantity { get; set; }
    public int? DurationDays { get; set; }
    public int Repeats { get; set; }
    public int? RepeatIntervalDays { get; set; }

    /// <summary>Assigned once at creation (PrescriptionRepository.GenerateScriptNumberAsync), never
    /// regenerated — one course of therapy keeps the same Rx number across every repeat dispense,
    /// matching real PBS practice and how an eRx token will need to work later.</summary>
    public string ScriptNumber { get; set; } = string.Empty;

    // Snapshot-copied from Medicine at creation time and never re-synced afterward — a script
    // shouldn't retroactively change appearance if the catalog entry is edited later.
    public bool IsPbsListed { get; set; }
    public bool IsRpbsListed { get; set; }
    public PbsRestrictionLevel? RestrictionLevel { get; set; }
    public MedicineSchedule? Schedule { get; set; }

    /// <summary>Authority/State-authority approval numbers, when RestrictionLevel/Schedule require
    /// them — free-text fields captured from the prescriber, not validated against a live Medicare
    /// Authority API (no such integration exists; same "capture the field, no network call" principle
    /// as the eRx seam).</summary>
    public string? AuthorityApprovalNumber { get; set; }
    public string? StateAuthorityCode { get; set; }

    public string? PrescribingNotes { get; set; }
    public string? NotesForPharmacy { get; set; }

    /// <summary>Free-text reason this was prescribed, captured by the wizard's Reason step — mirrors
    /// legacy Medinet's frmCatoDoc "Reason" picker, simplified to free text (no ICPC coding).</summary>
    public string? Reason { get; set; }

    /// <summary>Optional tag to an ongoing clinical issue, same Episode entity/relationship
    /// ProgressNote.EpisodeId already uses.</summary>
    public int? EpisodeId { get; set; }

    /// <summary>Which visit this was written during, when known — IntelliMed's deliberate improvement
    /// over Medinet, which carries no encounter/visit link on a script at all.</summary>
    public int? AppointmentId { get; set; }

    public int PrescribedByPractitionerId { get; set; }
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Active;

    /// <summary>Set when the prescribing wizard's safety check (IMedicationInteractionChecker) returned
    /// warnings and the prescriber explicitly confirmed proceeding anyway — the medico-legal record of
    /// "this warning was shown and consciously overridden," not just "never triggered."</summary>
    public bool SafetyOverrideAcknowledged { get; set; }
    public string? SafetyOverrideNote { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? CeasedDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public Medicine? Medicine { get; set; }
    public Appointment? Appointment { get; set; }
    public Practitioner? PrescribedByPractitioner { get; set; }
    public Episode? Episode { get; set; }
    public ICollection<PrescriptionIssue> Issues { get; set; } = new List<PrescriptionIssue>();
}

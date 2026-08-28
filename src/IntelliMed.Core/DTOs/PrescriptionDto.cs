using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class PrescriptionDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int? MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? Dose { get; set; }
    public string? Frequency { get; set; }
    public string? Instruction { get; set; }
    public string? Route { get; set; }
    public PrescriptionSourceCode? Purpose { get; set; }
    public bool IsLimitedDuration { get; set; }
    public string? Quantity { get; set; }
    public int? DurationDays { get; set; }
    public int Repeats { get; set; }
    public int? RepeatIntervalDays { get; set; }
    public string ScriptNumber { get; set; } = string.Empty;
    public bool IsPbsListed { get; set; }
    public bool IsRpbsListed { get; set; }
    public PbsRestrictionLevel? RestrictionLevel { get; set; }
    public MedicineSchedule? Schedule { get; set; }
    public string? AuthorityApprovalNumber { get; set; }
    public string? StateAuthorityCode { get; set; }
    public string? PrescribingNotes { get; set; }
    public string? NotesForPharmacy { get; set; }
    public string? Reason { get; set; }
    public int? EpisodeId { get; set; }
    public string? EpisodeTitle { get; set; }
    public int? AppointmentId { get; set; }
    public int PrescribedByPractitionerId { get; set; }
    public string PrescribedByPractitionerName { get; set; } = string.Empty;
    public PrescriptionStatus Status { get; set; }
    public bool SafetyOverrideAcknowledged { get; set; }
    public string? SafetyOverrideNote { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? CeasedDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Whether at least one PrescriptionIssue exists yet — drives the wizard/edit UI's
    /// lock state for Dose/Frequency/Route/Quantity/DurationDays/Repeats (see Prescription.cs's class
    /// doc comment). Computed by the repository, not stored.</summary>
    public bool IsIssued { get; set; }
}

public class CreatePrescriptionDto
{
    public int ClientId { get; set; }
    public int? MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? Dose { get; set; }
    public string? Frequency { get; set; }
    public string? Instruction { get; set; }
    public string? Route { get; set; }
    public PrescriptionSourceCode? Purpose { get; set; }
    public bool IsLimitedDuration { get; set; }
    public string? Quantity { get; set; }
    public int? DurationDays { get; set; }
    public int Repeats { get; set; }
    public int? RepeatIntervalDays { get; set; }
    public string? AuthorityApprovalNumber { get; set; }
    public string? StateAuthorityCode { get; set; }
    public string? PrescribingNotes { get; set; }
    public string? NotesForPharmacy { get; set; }
    public string? Reason { get; set; }
    public int? EpisodeId { get; set; }
    public int? AppointmentId { get; set; }
    public int PrescribedByPractitionerId { get; set; }
    public bool SafetyOverrideAcknowledged { get; set; }
    public string? SafetyOverrideNote { get; set; }

    /// <summary>Defaults to now (UTC) in the repository when left unset — see
    /// PrescriptionRepository.CreateAsync. The wizard's Dosage step always sets this explicitly.</summary>
    public DateTime? StartDate { get; set; }
}

/// <summary>Only the fields the repository will actually accept while the prescription is still
/// pre-issue, plus PrescribingNotes/NotesForPharmacy/Reason/EpisodeId which stay editable regardless —
/// see PrescriptionRepository.UpdateAsync for which of these are silently ignored once IsIssued is true.</summary>
public class UpdatePrescriptionDto
{
    public string? Dose { get; set; }
    public string? Frequency { get; set; }
    public string? Instruction { get; set; }
    public string? Route { get; set; }
    public PrescriptionSourceCode? Purpose { get; set; }
    public bool IsLimitedDuration { get; set; }
    public string? Quantity { get; set; }
    public int? DurationDays { get; set; }
    public int Repeats { get; set; }
    public int? RepeatIntervalDays { get; set; }
    public string? AuthorityApprovalNumber { get; set; }
    public string? StateAuthorityCode { get; set; }
    public string? PrescribingNotes { get; set; }
    public string? NotesForPharmacy { get; set; }
    public string? Reason { get; set; }
    public int? EpisodeId { get; set; }
}

/// <summary>Input to the prescribing wizard's live safety check — deliberately not tied to an
/// already-created Prescription, since the check happens before the prescriber has committed to
/// creating one.</summary>
public class CheckPrescriptionSafetyDto
{
    public int? MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? GenericName { get; set; }
}

/// <summary>Records who actually issued/printed a script and how — see
/// PrescriptionIssueRepository.IssueAsync for the snapshot fields this triggers.</summary>
public class IssuePrescriptionDto
{
    public int IssuedByPractitionerId { get; set; }
    public PrescriptionDeliveryMethod DeliveryMethod { get; set; } = PrescriptionDeliveryMethod.Paper;
}

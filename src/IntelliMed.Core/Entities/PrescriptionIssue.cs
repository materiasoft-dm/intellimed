namespace IntelliMed.Core.Entities;

/// <summary>Immutable record of one actual issue/print/reprint event — created only via
/// PrescriptionIssueRepository.IssueAsync, never updated or deleted afterward. Carries its own
/// snapshot of every field that can still change on the parent Prescription after this issue was
/// created (Dose/Frequency/Route/Quantity/Repeats/PrescribingNotes/NotesForPharmacy) plus prescriber
/// identity (not frozen anywhere else — the underlying Practitioner/ApplicationUser rows can change
/// later) — so reprinting an older issue always renders exactly what was actually issued at the time,
/// even if the parent Prescription or the prescriber's details have since changed. Fields that are
/// already frozen on Prescription itself at creation time (IsPbsListed/IsRpbsListed/RestrictionLevel/
/// Schedule) are deliberately NOT duplicated here — reading them off the (already-frozen) parent row
/// is equivalent, so PrescriptionPrint.razor joins to Prescription for those.</summary>
public class PrescriptionIssue
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public int IssuedByPractitionerId { get; set; }

    /// <summary>0 = the original issue; increments for each subsequent repeat re-issue of the same
    /// Prescription/ScriptNumber.</summary>
    public int RepeatNumber { get; set; }

    public PrescriptionDeliveryMethod DeliveryMethod { get; set; } = PrescriptionDeliveryMethod.Paper;
    public PrescriptionTransmissionStatus TransmissionStatus { get; set; } = PrescriptionTransmissionStatus.NotApplicable;

    // Snapshot at issue time — see class doc comment for why these specific fields are duplicated here
    // rather than read live off Prescription.
    public string? Dose { get; set; }
    public string? Frequency { get; set; }
    public string? Route { get; set; }
    public string? Quantity { get; set; }
    public int Repeats { get; set; }
    public string? PrescribingNotes { get; set; }
    public string? NotesForPharmacy { get; set; }

    public string PrescriberName { get; set; } = string.Empty;
    public string? PrescriberProviderNumber { get; set; }
    public string? PrescriberAhpraNumber { get; set; }
    public string? PrescriberHpiiNumber { get; set; }
    public string? PrescriberQualifications { get; set; }

    public Prescription? Prescription { get; set; }
    public Practitioner? IssuedByPractitioner { get; set; }
}

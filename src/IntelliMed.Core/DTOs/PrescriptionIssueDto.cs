using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

/// <summary>Deliberately flat/denormalized — includes client and script identity alongside the issue's
/// own snapshot fields, so PrescriptionPrint.razor can render a complete script from a single
/// GET api/prescription-issues/{id} call rather than threading clientId/prescriptionId through the
/// route to fetch each piece separately.</summary>
public class PrescriptionIssueDto
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public DateTime IssuedAt { get; set; }
    public int IssuedByPractitionerId { get; set; }
    public int RepeatNumber { get; set; }
    public PrescriptionDeliveryMethod DeliveryMethod { get; set; }
    public PrescriptionTransmissionStatus TransmissionStatus { get; set; }

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

    // Resolved from the parent Prescription/Client — these are already frozen at Prescription-creation
    // time (or never change, for client identity), so no separate snapshot is needed for them here.
    public string MedicineName { get; set; } = string.Empty;
    public string ScriptNumber { get; set; } = string.Empty;
    public bool IsPbsListed { get; set; }
    public bool IsRpbsListed { get; set; }
    public PbsRestrictionLevel? RestrictionLevel { get; set; }
    public MedicineSchedule? Schedule { get; set; }
    public string? AuthorityApprovalNumber { get; set; }
    public string? StateAuthorityCode { get; set; }

    public string ClientName { get; set; } = string.Empty;
    public DateTime? ClientDateOfBirth { get; set; }
    public string? ClientMedicareNumber { get; set; }
}

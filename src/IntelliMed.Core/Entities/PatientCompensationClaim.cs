namespace IntelliMed.Core.Entities;

public class PatientCompensationClaim
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string ClaimNum { get; set; } = string.Empty;
    public DateTime? DateOfInjury { get; set; }
    public string? EmployerName { get; set; }
    public string? CaseManagerName { get; set; }
    public string? PayerName { get; set; }
    public bool IsDefault { get; set; }
    public string? PublicNote { get; set; }
    public string? PrivateNote { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Patient? Patient { get; set; }
}

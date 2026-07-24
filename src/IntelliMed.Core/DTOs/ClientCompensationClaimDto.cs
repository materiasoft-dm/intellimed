namespace IntelliMed.Core.DTOs;

public class ClientCompensationClaimDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClaimNum { get; set; } = string.Empty;
    public DateTime? DateOfInjury { get; set; }
    public string? EmployerName { get; set; }
    public string? CaseManagerName { get; set; }
    public string? PayerName { get; set; }
    public bool IsDefault { get; set; }
    public string? PublicNote { get; set; }
    public string? PrivateNote { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateClientCompensationClaimDto
{
    public int ClientId { get; set; }
    public string ClaimNum { get; set; } = string.Empty;
    public DateTime? DateOfInjury { get; set; }
    public string? EmployerName { get; set; }
    public string? CaseManagerName { get; set; }
    public string? PayerName { get; set; }
    public bool IsDefault { get; set; }
    public string? PublicNote { get; set; }
    public string? PrivateNote { get; set; }
}

public class UpdateClientCompensationClaimDto
{
    public string ClaimNum { get; set; } = string.Empty;
    public DateTime? DateOfInjury { get; set; }
    public string? EmployerName { get; set; }
    public string? CaseManagerName { get; set; }
    public string? PayerName { get; set; }
    public bool IsDefault { get; set; }
    public string? PublicNote { get; set; }
    public string? PrivateNote { get; set; }
    public bool IsArchived { get; set; }
}

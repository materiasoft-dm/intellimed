namespace IntelliMed.Core.DTOs;

public class ClientReferralDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public DateTime ReferralDate { get; set; }
    public string? ReferralPeriod { get; set; }
    public bool IsDefault { get; set; }
    public bool IsGP { get; set; }
    public string ReferringProviderName { get; set; } = string.Empty;
    public string? ReferringProviderNumber { get; set; }
    public string? RequestTypeCde { get; set; }
    public DateTime? FirstVisitDate { get; set; }
    public string? Note { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateClientReferralDto
{
    public int ClientId { get; set; }
    public DateTime ReferralDate { get; set; }
    public string? ReferralPeriod { get; set; }
    public bool IsDefault { get; set; }
    public bool IsGP { get; set; }
    public string ReferringProviderName { get; set; } = string.Empty;
    public string? ReferringProviderNumber { get; set; }
    public string? RequestTypeCde { get; set; }
    public DateTime? FirstVisitDate { get; set; }
    public string? Note { get; set; }
}

public class UpdateClientReferralDto
{
    public DateTime ReferralDate { get; set; }
    public string? ReferralPeriod { get; set; }
    public bool IsDefault { get; set; }
    public bool IsGP { get; set; }
    public string ReferringProviderName { get; set; } = string.Empty;
    public string? ReferringProviderNumber { get; set; }
    public string? RequestTypeCde { get; set; }
    public DateTime? FirstVisitDate { get; set; }
    public string? Note { get; set; }
    public bool IsArchived { get; set; }
}

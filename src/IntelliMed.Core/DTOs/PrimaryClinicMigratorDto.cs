namespace IntelliMed.Core.DTOs;

/// <summary>Result of one PrimaryClinic Migrator run — created/updated counts per entity type, keyed by LegacyGuid for idempotent re-runs.</summary>
public class PrimaryClinicMigratorResultDto
{
    public int ClientsCreated { get; set; }
    public int ClientsUpdated { get; set; }
    public int AddressesCreated { get; set; }
    public int AddressesUpdated { get; set; }
    public int ReferralsCreated { get; set; }
    public int ReferralsUpdated { get; set; }
    public int OccupationsCreated { get; set; }
    public int OccupationsUpdated { get; set; }
    public int FamilyRelationshipsCreated { get; set; }
    public int FamilyRelationshipsUpdated { get; set; }
    public int CompensationClaimsCreated { get; set; }
    public int CompensationClaimsUpdated { get; set; }
    public int HealthFundsCreated { get; set; }
    public int InvoicesCreated { get; set; }
    public int InvoicesUpdated { get; set; }
    public int InvoiceItemsCreated { get; set; }
    public int InvoiceItemsUpdated { get; set; }
    public int PaymentsCreated { get; set; }
    public int PaymentsUpdated { get; set; }
    public List<string> Warnings { get; set; } = new();
}

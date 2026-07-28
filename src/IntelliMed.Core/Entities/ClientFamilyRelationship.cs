namespace IntelliMed.Core.Entities;

public class ClientFamilyRelationship
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int RelativeClientId { get; set; }
    public string? RelationshipType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The legacy Pracnet FamilyRelationships.GUID this record was imported from (PrimaryClinic Migrator).</summary>
    public string? LegacyGuid { get; set; }

    public Client? Client { get; set; }
    public Client? RelativeClient { get; set; }
}

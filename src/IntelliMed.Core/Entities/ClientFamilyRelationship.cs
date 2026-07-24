namespace IntelliMed.Core.Entities;

public class ClientFamilyRelationship
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int RelativeClientId { get; set; }
    public string? RelationshipType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Client? Client { get; set; }
    public Client? RelativeClient { get; set; }
}

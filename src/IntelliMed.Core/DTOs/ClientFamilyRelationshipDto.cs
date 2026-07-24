namespace IntelliMed.Core.DTOs;

public class ClientFamilyRelationshipDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int RelativeClientId { get; set; }
    public string RelativeName { get; set; } = string.Empty;
    public string RelativeAddress { get; set; } = string.Empty;
    public string? RelationshipType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateClientFamilyRelationshipDto
{
    public int ClientId { get; set; }
    public int RelativeClientId { get; set; }
    public string? RelationshipType { get; set; }
}

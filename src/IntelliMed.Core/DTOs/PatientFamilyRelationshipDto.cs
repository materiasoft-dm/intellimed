namespace IntelliMed.Core.DTOs;

public class PatientFamilyRelationshipDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int RelativePatientId { get; set; }
    public string RelativeName { get; set; } = string.Empty;
    public string RelativeAddress { get; set; } = string.Empty;
    public string? RelationshipType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePatientFamilyRelationshipDto
{
    public int PatientId { get; set; }
    public int RelativePatientId { get; set; }
    public string? RelationshipType { get; set; }
}

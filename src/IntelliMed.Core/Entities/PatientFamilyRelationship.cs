namespace IntelliMed.Core.Entities;

public class PatientFamilyRelationship
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int RelativePatientId { get; set; }
    public string? RelationshipType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Patient? Patient { get; set; }
    public Patient? RelativePatient { get; set; }
}

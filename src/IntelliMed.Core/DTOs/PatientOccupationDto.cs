namespace IntelliMed.Core.DTOs;

public class PatientOccupationDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string? Occupation { get; set; }
    public string? Employer { get; set; }
    public int? StartedYear { get; set; }
    public int? StoppedYear { get; set; }
    public bool HasAsbestos { get; set; }
    public bool HasDust { get; set; }
    public bool HasRadiation { get; set; }
    public bool HasAnimals { get; set; }
    public string? Comment { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreatePatientOccupationDto
{
    public int PatientId { get; set; }
    public string? Occupation { get; set; }
    public string? Employer { get; set; }
    public int? StartedYear { get; set; }
    public int? StoppedYear { get; set; }
    public bool HasAsbestos { get; set; }
    public bool HasDust { get; set; }
    public bool HasRadiation { get; set; }
    public bool HasAnimals { get; set; }
    public string? Comment { get; set; }
}

public class UpdatePatientOccupationDto
{
    public string? Occupation { get; set; }
    public string? Employer { get; set; }
    public int? StartedYear { get; set; }
    public int? StoppedYear { get; set; }
    public bool HasAsbestos { get; set; }
    public bool HasDust { get; set; }
    public bool HasRadiation { get; set; }
    public bool HasAnimals { get; set; }
    public string? Comment { get; set; }
    public bool IsArchived { get; set; }
}

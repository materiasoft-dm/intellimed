namespace IntelliMed.Core.DTOs;

public class ClientOccupationDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string? Occupation { get; set; }
    /// <summary>An organisation client.</summary>
    public int? EmployerClientId { get; set; }
    /// <summary>The organisation client's name (read-only).</summary>
    public string? EmployerName { get; set; }
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

public class CreateClientOccupationDto
{
    public int ClientId { get; set; }
    public string? Occupation { get; set; }
    /// <summary>An organisation client.</summary>
    public int? EmployerClientId { get; set; }
    public int? StartedYear { get; set; }
    public int? StoppedYear { get; set; }
    public bool HasAsbestos { get; set; }
    public bool HasDust { get; set; }
    public bool HasRadiation { get; set; }
    public bool HasAnimals { get; set; }
    public string? Comment { get; set; }
}

public class UpdateClientOccupationDto
{
    public string? Occupation { get; set; }
    /// <summary>An organisation client.</summary>
    public int? EmployerClientId { get; set; }
    public int? StartedYear { get; set; }
    public int? StoppedYear { get; set; }
    public bool HasAsbestos { get; set; }
    public bool HasDust { get; set; }
    public bool HasRadiation { get; set; }
    public bool HasAnimals { get; set; }
    public string? Comment { get; set; }
    public bool IsArchived { get; set; }
}

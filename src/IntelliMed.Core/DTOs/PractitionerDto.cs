namespace IntelliMed.Core.DTOs;

public class PractitionerDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{Title} {FirstName} {LastName}".Trim();
    public string Title { get; set; } = string.Empty;
    public string Profession { get; set; } = string.Empty;
    public string? ProviderNumber { get; set; }
    public string? AhpraNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreatePractitionerDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Profession { get; set; } = string.Empty;
    public string? ProviderNumber { get; set; }
    public string? AhpraNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class UpdatePractitionerDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Profession { get; set; } = string.Empty;
    public string? ProviderNumber { get; set; }
    public string? AhpraNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
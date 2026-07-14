namespace IntelliMed.Core.Entities;

public class Practitioner
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Profession { get; set; } = string.Empty;
    public string? ProviderNumber { get; set; }
    public string? AhpraNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
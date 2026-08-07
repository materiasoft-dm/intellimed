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
    public ProviderServiceType ServiceType { get; set; } = ProviderServiceType.GeneralPractitioner;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The staff login (if any) this practitioner corresponds to, set by an admin from User
    /// Management. Drives per-provider patient visibility — a user linked here who is not
    /// SuperAdmin/Admin only sees patients assigned to this practitioner. Null means unlinked
    /// (e.g. a visiting specialist with no system login, or not yet configured by an admin).</summary>
    public string? ApplicationUserId { get; set; }

    // Navigation property
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
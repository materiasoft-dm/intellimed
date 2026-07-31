namespace IntelliMed.Core.Entities;

public class Notification
{
    public int Id { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ApplicationUser? RecipientUser { get; set; }
}

public enum NotificationType
{
    AppointmentBooked,
    AppointmentRescheduled,
    RoleAssignmentChanged
}

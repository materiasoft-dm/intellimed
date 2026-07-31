using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UnreadCountDto
{
    public int Count { get; set; }
}

/// <summary>Internal-only — passed between a triggering controller/service and INotificationRepository, never bound from an HTTP request body.</summary>
public class CreateNotificationDto
{
    public string RecipientUserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
}

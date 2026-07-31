using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface INotificationService
{
    Task<IEnumerable<NotificationDto>> GetRecentAsync(int take = 20);
    Task<int> GetUnreadCountAsync();
    Task<bool> MarkReadAsync(int id);
    Task<bool> MarkAllReadAsync();
}

using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface INotificationRepository : IRepository<Notification>
{
    Task<IEnumerable<NotificationDto>> GetMyRecentAsync(string userId, int take = 20);
    Task<int> GetUnreadCountAsync(string userId);
    Task<bool> MarkReadAsync(int id, string userId);
    Task<int> MarkAllReadAsync(string userId);
    Task<int> CreateAsync(CreateNotificationDto dto);
}

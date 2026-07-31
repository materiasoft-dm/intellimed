using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class NotificationRepository : Repository<Notification>, INotificationRepository
{
    public NotificationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<NotificationDto>> GetMyRecentAsync(string userId, int take = 20)
    {
        var notifications = await _dbSet
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
        return notifications.Select(EntityMapper.ToDto);
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _dbSet.CountAsync(n => n.RecipientUserId == userId && !n.IsRead);
    }

    public async Task<bool> MarkReadAsync(int id, string userId)
    {
        var notification = await _dbSet.FindAsync(id);
        if (notification == null || notification.RecipientUserId != userId)
            return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<int> MarkAllReadAsync(string userId)
    {
        var unread = await _dbSet
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        if (unread.Count > 0)
            await _context.SaveChangesAsync();

        return unread.Count;
    }

    public async Task<int> CreateAsync(CreateNotificationDto dto)
    {
        var notification = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(notification);
        await _context.SaveChangesAsync();
        return notification.Id;
    }
}

using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class NotificationRepositoryTests : IDisposable
{
    private readonly NotificationRepository _repository;
    private readonly AppDbContext _context;
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    public NotificationRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new NotificationRepository(_context);

        _context.Users.Add(new ApplicationUser { Id = UserId, Email = "user1@test.com", UserName = "user1@test.com" });
        _context.Users.Add(new ApplicationUser { Id = OtherUserId, Email = "user2@test.com", UserName = "user2@test.com" });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewNotificationId()
    {
        var dto = new CreateNotificationDto { RecipientUserId = UserId, Type = NotificationType.AppointmentBooked, Message = "Test" };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var notification = await _context.Notifications.FindAsync(result);
        notification!.Message.Should().Be("Test");
        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task GetMyRecentAsync_ReturnsOnlyThatUsersNotifications_NewestFirst()
    {
        _context.Notifications.Add(new Notification { RecipientUserId = UserId, Message = "First", CreatedAt = DateTime.UtcNow.AddMinutes(-10) });
        _context.Notifications.Add(new Notification { RecipientUserId = UserId, Message = "Second", CreatedAt = DateTime.UtcNow });
        _context.Notifications.Add(new Notification { RecipientUserId = OtherUserId, Message = "NotMine", CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetMyRecentAsync(UserId)).ToList();

        result.Should().HaveCount(2);
        result[0].Message.Should().Be("Second");
        result[1].Message.Should().Be("First");
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyUnreadForThatUser()
    {
        _context.Notifications.Add(new Notification { RecipientUserId = UserId, Message = "Unread1", IsRead = false });
        _context.Notifications.Add(new Notification { RecipientUserId = UserId, Message = "Read1", IsRead = true });
        _context.Notifications.Add(new Notification { RecipientUserId = OtherUserId, Message = "NotMine", IsRead = false });
        await _context.SaveChangesAsync();

        var count = await _repository.GetUnreadCountAsync(UserId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task MarkReadAsync_OwnedNotification_MarksReadAndReturnsTrue()
    {
        var notification = new Notification { RecipientUserId = UserId, Message = "Test" };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        var result = await _repository.MarkReadAsync(notification.Id, UserId);

        result.Should().BeTrue();
        var updated = await _context.Notifications.FindAsync(notification.Id);
        updated!.IsRead.Should().BeTrue();
        updated.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkReadAsync_NotOwnedByCaller_ReturnsFalseAndLeavesUnread()
    {
        var notification = new Notification { RecipientUserId = OtherUserId, Message = "Test" };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        var result = await _repository.MarkReadAsync(notification.Id, UserId);

        result.Should().BeFalse();
        var updated = await _context.Notifications.FindAsync(notification.Id);
        updated!.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAllReadAsync_MarksOnlyThatUsersUnreadNotifications()
    {
        _context.Notifications.Add(new Notification { RecipientUserId = UserId, Message = "Unread1", IsRead = false });
        _context.Notifications.Add(new Notification { RecipientUserId = UserId, Message = "Unread2", IsRead = false });
        _context.Notifications.Add(new Notification { RecipientUserId = OtherUserId, Message = "NotMine", IsRead = false });
        await _context.SaveChangesAsync();

        var count = await _repository.MarkAllReadAsync(UserId);

        count.Should().Be(2);
        (await _repository.GetUnreadCountAsync(UserId)).Should().Be(0);
        (await _repository.GetUnreadCountAsync(OtherUserId)).Should().Be(1);
    }
}

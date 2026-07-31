using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Services;
using IntelliMed.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace IntelliMed.Tests.Services;

public class AppointmentReminderServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<INotificationRepository> _notificationRepositoryMock = new();
    private readonly AppointmentReminderService _service;

    public AppointmentReminderServiceTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _service = new AppointmentReminderService(_context, _notificationRepositoryMock.Object, NullLogger<AppointmentReminderService>.Instance);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task NotifyUpcomingAsync_PractitionerHasLinkedLogin_CreatesNotification()
    {
        var practitioner = new Practitioner { FirstName = "Jane", LastName = "Doe", Email = "jane.doe@clinic.com" };
        _context.Practitioners.Add(practitioner);
        _context.Users.Add(new ApplicationUser { Id = "linked-user", Email = "jane.doe@clinic.com", UserName = "jane.doe@clinic.com" });
        await _context.SaveChangesAsync();

        var appointment = new AppointmentDto
        {
            Id = 42,
            PractitionerId = practitioner.Id,
            ClientName = "John Smith",
            AppointmentDate = DateTime.Today
        };

        await _service.NotifyUpcomingAsync(appointment);

        _notificationRepositoryMock.Verify(r => r.CreateAsync(It.Is<CreateNotificationDto>(
            dto => dto.RecipientUserId == "linked-user" &&
                   dto.Type == NotificationType.AppointmentBooked &&
                   dto.LinkUrl == "/appointments/edit/42")), Times.Once);
    }

    [Fact]
    public async Task NotifyUpcomingAsync_PractitionerHasNoLinkedLogin_DoesNotCreateNotification()
    {
        var practitioner = new Practitioner { FirstName = "No", LastName = "Login", Email = "no.login@clinic.com" };
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();

        var appointment = new AppointmentDto
        {
            Id = 43,
            PractitionerId = practitioner.Id,
            ClientName = "Jane Smith",
            AppointmentDate = DateTime.Today
        };

        await _service.NotifyUpcomingAsync(appointment);

        _notificationRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<CreateNotificationDto>()), Times.Never);
    }

    [Fact]
    public async Task NotifyUpcomingAsync_UnknownPractitioner_DoesNotCreateNotification()
    {
        var appointment = new AppointmentDto
        {
            Id = 44,
            PractitionerId = 9999,
            ClientName = "Unknown Practitioner Case",
            AppointmentDate = DateTime.Today
        };

        await _service.NotifyUpcomingAsync(appointment);

        _notificationRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<CreateNotificationDto>()), Times.Never);
    }
}

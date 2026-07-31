using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Creates an in-app notification for the practitioner when one of their appointments is booked or
/// rescheduled. There is no FK between Practitioner and ApplicationUser, so the practitioner's login
/// is soft-matched by email — the same lookup ProviderScheduleRepository.GetByPractitionerEmailAsync
/// already uses. No match (e.g. the practitioner has no staff login) is treated as "no opinion": log
/// and return, same as that method, rather than failing the booking/reschedule request.
/// </summary>
public class AppointmentReminderService : IAppointmentReminderService
{
    private readonly AppDbContext _context;
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<AppointmentReminderService> _logger;

    public AppointmentReminderService(
        AppDbContext context,
        INotificationRepository notificationRepository,
        ILogger<AppointmentReminderService> logger)
    {
        _context = context;
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    public async Task NotifyUpcomingAsync(AppointmentDto appointment)
    {
        var practitionerEmail = await _context.Practitioners
            .Where(p => p.Id == appointment.PractitionerId)
            .Select(p => p.Email)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(practitionerEmail))
            return;

        var userId = await _context.Users
            .Where(u => u.Email == practitionerEmail)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userId == null)
        {
            _logger.LogDebug(
                "No linked staff login for practitioner {PractitionerId} — skipping in-app notification",
                appointment.PractitionerId);
            return;
        }

        await _notificationRepository.CreateAsync(new CreateNotificationDto
        {
            RecipientUserId = userId,
            Type = NotificationType.AppointmentBooked,
            Message = $"Appointment with {appointment.ClientName} on {appointment.AppointmentDate:d} at {appointment.StartTimeFormatted}",
            LinkUrl = $"/appointments/edit/{appointment.Id}"
        });
    }
}

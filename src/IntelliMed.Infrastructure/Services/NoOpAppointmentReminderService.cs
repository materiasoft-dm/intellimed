using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace IntelliMed.Infrastructure.Services;

/// <summary>Stub reminder hook — logs intent only. Swap for a real SMS/email implementation when that infrastructure exists.</summary>
public class NoOpAppointmentReminderService : IAppointmentReminderService
{
    private readonly ILogger<NoOpAppointmentReminderService> _logger;

    public NoOpAppointmentReminderService(ILogger<NoOpAppointmentReminderService> logger)
    {
        _logger = logger;
    }

    public Task NotifyUpcomingAsync(AppointmentDto appointment)
    {
        _logger.LogInformation(
            "Reminder stub: would notify {ClientName} about their appointment on {Date} at {Time}",
            appointment.ClientName, appointment.AppointmentDate.ToShortDateString(), appointment.StartTimeFormatted);
        return Task.CompletedTask;
    }
}

using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

/// <summary>
/// Hook for notifying a client about an upcoming or just-changed appointment.
/// </summary>
public interface IAppointmentReminderService
{
    Task NotifyUpcomingAsync(AppointmentDto appointment);
}

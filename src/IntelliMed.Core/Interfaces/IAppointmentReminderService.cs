using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

/// <summary>
/// Hook for notifying a client about an upcoming or just-changed appointment. No real SMS/email
/// infrastructure exists in this app yet — see NoOpAppointmentReminderService for the stub implementation.
/// </summary>
public interface IAppointmentReminderService
{
    Task NotifyUpcomingAsync(AppointmentDto appointment);
}

using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Web.Services;

public interface IAppointmentService
{
    Task<PagedResult<AppointmentDto>> SearchAppointmentsAsync(AppointmentSearchDto search);
    Task<List<AppointmentDto>> GetWaitingRoomAsync(DateTime? date = null);
    Task<AppointmentDto?> GetAppointmentByIdAsync(int id);
    Task<List<AppointmentDto>> GetSeriesAsync(Guid seriesId);
    Task<bool> CheckAvailabilityAsync(int practitionerId, DateTime date, TimeSpan startTime, TimeSpan endTime, int? excludeAppointmentId = null);
    Task<ProviderScheduleDayDto?> GetPractitionerScheduleAsync(int practitionerId, DateTime date);
    Task<int?> CreateAppointmentAsync(CreateAppointmentDto dto);
    Task<List<int>?> CreateAppointmentSeriesAsync(CreateAppointmentSeriesDto dto);
    Task<bool> UpdateAppointmentAsync(int id, UpdateAppointmentDto dto);
    Task<bool> UpdateStatusAsync(int id, AppointmentStatus status);
    Task<(bool Success, string? Error)> RescheduleAsync(int id, RescheduleAppointmentDto dto);
    Task<bool> DeleteAppointmentAsync(int id);
    Task<int> DeleteSeriesAsync(Guid seriesId, DateTime? fromDate = null);
}

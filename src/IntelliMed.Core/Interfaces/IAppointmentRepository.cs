using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<AppointmentDto?> GetByIdAsync(int id);
    Task<IEnumerable<AppointmentDto>> SearchAsync(AppointmentSearchDto search);
    Task<(IEnumerable<AppointmentDto> Items, int TotalCount)> GetPagedAsync(AppointmentSearchDto search);
    Task<IEnumerable<AppointmentDto>> GetByDateAsync(DateTime date);
    Task<IEnumerable<AppointmentDto>> GetByClientIdAsync(int clientId);
    Task<IEnumerable<AppointmentDto>> GetByPractitionerIdAsync(int practitionerId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<int> CreateAsync(CreateAppointmentDto dto);
    Task<IEnumerable<int>> CreateSeriesAsync(CreateAppointmentSeriesDto dto);
    Task UpdateAsync(int id, UpdateAppointmentDto dto);
    Task UpdateStatusAsync(int id, AppointmentStatus status);
    Task RescheduleAsync(int id, RescheduleAppointmentDto dto);
    Task<bool> IsTimeSlotAvailableAsync(int practitionerId, DateTime date, TimeSpan startTime, TimeSpan endTime, int? excludeAppointmentId = null);
    Task<IEnumerable<AppointmentDto>> GetWaitingRoomAsync(int? clinicId, DateTime date);
    Task<IEnumerable<AppointmentDto>> GetBySeriesIdAsync(Guid seriesId);
    Task<int> DeleteSeriesAsync(Guid seriesId, DateTime? fromDate = null);
}
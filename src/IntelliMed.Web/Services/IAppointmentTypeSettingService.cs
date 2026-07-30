using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IAppointmentTypeSettingService
{
    Task<List<AppointmentTypeSettingDto>> GetAllActiveAsync();
    Task<int?> CreateAsync(CreateAppointmentTypeSettingDto dto);
    Task<bool> UpdateAsync(int id, UpdateAppointmentTypeSettingDto dto);
    Task<bool> ArchiveAsync(int id);
}

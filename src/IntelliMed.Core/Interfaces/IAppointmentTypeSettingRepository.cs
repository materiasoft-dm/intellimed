using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IAppointmentTypeSettingRepository : IRepository<AppointmentTypeSetting>
{
    Task<AppointmentTypeSettingDto?> GetByIdAsync(int id);
    Task<IEnumerable<AppointmentTypeSettingDto>> GetAllActiveAsync(int? clinicId);
    Task<int> CreateAsync(CreateAppointmentTypeSettingDto dto);
    Task UpdateAsync(int id, UpdateAppointmentTypeSettingDto dto);
    Task ArchiveAsync(int id);
}

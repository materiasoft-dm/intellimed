using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IClinicSettingsRepository : IRepository<ClinicSettings>
{
    Task<ClinicSettingsDto> GetSingletonAsync();
    Task UpdateSingletonAsync(UpdateClinicSettingsRequest request);
}

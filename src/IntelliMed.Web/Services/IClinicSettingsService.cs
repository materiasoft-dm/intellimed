using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IClinicSettingsService
{
    Task<ClinicSettingsDto?> GetAsync();
    Task<bool> UpdateAsync(UpdateClinicSettingsRequest request);
}

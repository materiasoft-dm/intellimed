using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

public interface ISystemSettingsRepository
{
    /// <summary>Always returns settings — falls back to a defaults instance if the seeded row is
    /// somehow missing, so callers (Identity configuration, token issue, backup pruning) never have
    /// to handle a null and never silently lose their policy.</summary>
    Task<SystemSettingsDto> GetAsync();

    Task UpdateAsync(UpdateSystemSettingsDto dto);
}

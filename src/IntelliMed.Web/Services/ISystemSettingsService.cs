using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface ISystemSettingsService
{
    Task<SystemSettingsDto?> GetAsync();

    /// <summary>Returns null on success, or the server's validation message when a value was out of
    /// range — the settings screen shows it verbatim rather than a generic failure.</summary>
    Task<string?> UpdateAsync(UpdateSystemSettingsDto dto);
}

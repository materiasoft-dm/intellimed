using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IDatabaseBackupSettingsRepository : IRepository<DatabaseBackupSettings>
{
    Task<DatabaseBackupSettingsDto> GetSingletonAsync();
    Task UpdateSingletonAsync(UpdateDatabaseBackupSettingsDto dto);

    /// <summary>Stamps LastRunAt after a scheduled (not manual) run, so the background service knows when the next one is due.</summary>
    Task MarkRanAsync(DateTime ranAt);
}

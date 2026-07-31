using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IDatabaseBackupService
{
    Task<IEnumerable<DatabaseBackupDto>> GetAllAsync();
    Task<DatabaseBackupSettingsDto?> GetSettingsAsync();
    Task<bool> UpdateSettingsAsync(UpdateDatabaseBackupSettingsDto dto);
    Task<bool> BackupNowAsync();
    Task<byte[]?> DownloadAsync(int id);
}

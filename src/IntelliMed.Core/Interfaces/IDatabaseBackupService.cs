using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

/// <summary>
/// Performs the actual SQLite backup (file I/O) and reports on what's on disk. The filesystem is the
/// source of truth — there is no backing table, so every backup is identified by its FileName.
/// </summary>
public interface IDatabaseBackupService
{
    Task<IEnumerable<DatabaseBackupDto>> GetAllAsync();
    Task<DatabaseBackupDto> PerformBackupAsync(DatabaseBackupTrigger trigger);
    Task<(byte[] Content, string FileName)?> GetBackupFileAsync(string fileName);
    Task DeleteAsync(string fileName);
    Task<int> DeleteExpiredAsync(DateTime olderThan);
}

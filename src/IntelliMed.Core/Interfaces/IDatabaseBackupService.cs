using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

/// <summary>Performs the actual SQLite backup (file I/O), as distinct from IDatabaseBackupRepository which only persists backup records.</summary>
public interface IDatabaseBackupService
{
    Task<DatabaseBackupDto> PerformBackupAsync(DatabaseBackupTrigger trigger);
    Task<(byte[] Content, string FileName)?> GetBackupFileAsync(int id);
}

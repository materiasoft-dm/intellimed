using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IDatabaseBackupRepository : IRepository<DatabaseBackup>
{
    Task<IEnumerable<DatabaseBackupDto>> GetAllAsync();
    Task<DatabaseBackupDto?> GetByIdAsync(int id);
    Task<int> CreateAsync(CreateDatabaseBackupDto dto);
}

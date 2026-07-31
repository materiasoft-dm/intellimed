using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class DatabaseBackupRepository : Repository<DatabaseBackup>, IDatabaseBackupRepository
{
    public DatabaseBackupRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<DatabaseBackupDto>> GetAllAsync()
    {
        var backups = await _dbSet
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
        return backups.Select(EntityMapper.ToDto);
    }

    public async Task<DatabaseBackupDto?> GetByIdAsync(int id)
    {
        var backup = await _dbSet.FindAsync(id);
        return backup == null ? null : EntityMapper.ToDto(backup);
    }

    public async Task<int> CreateAsync(CreateDatabaseBackupDto dto)
    {
        var backup = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(backup);
        await _context.SaveChangesAsync();
        return backup.Id;
    }
}

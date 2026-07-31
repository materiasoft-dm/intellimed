using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class DatabaseBackupSettingsRepository : Repository<DatabaseBackupSettings>, IDatabaseBackupSettingsRepository
{
    public DatabaseBackupSettingsRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<DatabaseBackupSettingsDto> GetSingletonAsync()
    {
        var settings = await _dbSet.SingleAsync(s => s.Id == 1);
        return new DatabaseBackupSettingsDto
        {
            IntervalValue = settings.IntervalValue,
            IntervalUnit = settings.IntervalUnit,
            LastRunAt = settings.LastRunAt
        };
    }

    public async Task UpdateSingletonAsync(UpdateDatabaseBackupSettingsDto dto)
    {
        var settings = await _dbSet.SingleAsync(s => s.Id == 1);
        settings.IntervalValue = dto.IntervalValue;
        settings.IntervalUnit = dto.IntervalUnit;
        await _context.SaveChangesAsync();
    }

    public async Task MarkRanAsync(DateTime ranAt)
    {
        var settings = await _dbSet.SingleAsync(s => s.Id == 1);
        settings.LastRunAt = ranAt;
        await _context.SaveChangesAsync();
    }
}

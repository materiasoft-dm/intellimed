using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class AppointmentTypeSettingRepository : Repository<AppointmentTypeSetting>, IAppointmentTypeSettingRepository
{
    public AppointmentTypeSettingRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<AppointmentTypeSettingDto?> GetByIdAsync(int id)
    {
        var setting = await _dbSet.FirstOrDefaultAsync(s => s.Id == id);
        return setting == null ? null : EntityMapper.ToDto(setting);
    }

    public async Task<IEnumerable<AppointmentTypeSettingDto>> GetAllActiveAsync(int? clinicId)
    {
        var query = _dbSet.Where(s => s.IsActive);
        if (clinicId.HasValue)
            query = query.Where(s => s.ClinicId == clinicId.Value);

        var settings = await query
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();
        return settings.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateAppointmentTypeSettingDto dto)
    {
        var setting = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(setting);
        await _context.SaveChangesAsync();
        return setting.Id;
    }

    public async Task UpdateAsync(int id, UpdateAppointmentTypeSettingDto dto)
    {
        var setting = await _dbSet.FindAsync(id);
        if (setting == null)
            throw new InvalidOperationException($"AppointmentTypeSetting with ID {id} not found");

        EntityMapper.UpdateEntity(setting, dto);
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var setting = await _dbSet.FindAsync(id);
        if (setting == null)
            throw new InvalidOperationException($"AppointmentTypeSetting with ID {id} not found");

        setting.IsActive = false;
        await _context.SaveChangesAsync();
    }
}

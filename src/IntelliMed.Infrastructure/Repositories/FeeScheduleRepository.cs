using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class FeeScheduleRepository : Repository<FeeSchedule>, IFeeScheduleRepository
{
    public FeeScheduleRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<FeeScheduleDto?> GetByIdAsync(int id)
    {
        var schedule = await _dbSet
            .Include(f => f.HealthFund)
            .Include(f => f.FeeTable)
            .FirstOrDefaultAsync(f => f.Id == id);
        return schedule == null ? null : EntityMapper.ToDto(schedule);
    }

    public async Task<IEnumerable<FeeScheduleDto>> GetAllActiveAsync()
    {
        var schedules = await _dbSet
            .Include(f => f.HealthFund)
            .Include(f => f.FeeTable)
            .Where(f => !f.IsArchived)
            .OrderBy(f => f.Code)
            .ToListAsync();
        return schedules.Select(EntityMapper.ToDto);
    }

    public async Task<(IEnumerable<FeeScheduleDto> Items, int TotalCount)> GetPagedAsync(FeeScheduleSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var totalCount = await query.CountAsync();

        var schedules = await query
            .Include(f => f.HealthFund)
            .Include(f => f.FeeTable)
            .OrderBy(f => f.Code)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync();

        return (schedules.Select(EntityMapper.ToDto), totalCount);
    }

    public async Task<int> CreateAsync(CreateFeeScheduleDto dto)
    {
        var schedule = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(schedule);
        await _context.SaveChangesAsync();
        return schedule.Id;
    }

    public async Task UpdateAsync(int id, UpdateFeeScheduleDto dto)
    {
        var schedule = await _dbSet.FindAsync(id);
        if (schedule == null)
            throw new InvalidOperationException($"FeeSchedule with ID {id} not found");

        EntityMapper.UpdateEntity(schedule, dto);
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var schedule = await _dbSet.FindAsync(id);
        if (schedule == null)
            throw new InvalidOperationException($"FeeSchedule with ID {id} not found");

        schedule.IsArchived = true;
        schedule.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    private IQueryable<FeeSchedule> BuildSearchQuery(FeeScheduleSearchDto search)
    {
        var query = _dbSet.AsQueryable();

        if (!search.IncludeArchived)
            query = query.Where(f => !f.IsArchived);

        if (!string.IsNullOrWhiteSpace(search.Code))
            query = query.Where(f => f.Code.ToLower().Contains(search.Code.ToLower()));

        if (!string.IsNullOrWhiteSpace(search.Description))
            query = query.Where(f => f.Description.ToLower().Contains(search.Description.ToLower()));

        return query;
    }
}

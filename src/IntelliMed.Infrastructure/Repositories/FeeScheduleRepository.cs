using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using IntelliMed.Infrastructure.Services;
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

    public async Task<IEnumerable<FeeScheduleItemDto>> GetItemsAsync(int feeScheduleId)
    {
        var items = await _context.FeeScheduleItems
            .Include(i => i.BillingItem)
            .Where(i => i.FeeScheduleId == feeScheduleId)
            .OrderBy(i => i.BillingItem!.ItemNumber)
            .ToListAsync();
        return items.Select(EntityMapper.ToDto);
    }

    public async Task<int> AddItemAsync(int feeScheduleId, CreateFeeScheduleItemDto dto)
    {
        var item = new FeeScheduleItem
        {
            FeeScheduleId = feeScheduleId,
            BillingItemId = dto.BillingItemId,
            Fee = dto.Fee,
            CreatedAt = DateTime.UtcNow
        };
        await _context.FeeScheduleItems.AddAsync(item);
        await _context.SaveChangesAsync();
        return item.Id;
    }

    public async Task UpdateItemFeeAsync(int itemId, decimal fee)
    {
        var item = await _context.FeeScheduleItems.FindAsync(itemId);
        if (item == null)
            throw new InvalidOperationException($"FeeScheduleItem with ID {itemId} not found");

        item.Fee = fee;
        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task RemoveItemAsync(int itemId)
    {
        var item = await _context.FeeScheduleItems.FindAsync(itemId);
        if (item != null)
        {
            _context.FeeScheduleItems.Remove(item);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> CopyFeesFromMbsAsync(int feeScheduleId)
    {
        var items = await _context.FeeScheduleItems
            .Include(i => i.BillingItem)
            .Where(i => i.FeeScheduleId == feeScheduleId)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var updated = 0;
        foreach (var item in items)
        {
            if (item.BillingItem != null && item.Fee != item.BillingItem.ScheduleFee)
            {
                item.Fee = item.BillingItem.ScheduleFee;
                item.UpdatedAt = now;
                updated++;
            }
        }

        if (updated > 0)
            await _context.SaveChangesAsync();

        return updated;
    }

    /// <summary>
    /// Bulk-populates this schedule's items either from another schedule's item-fee list, or (when
    /// SourceFeeScheduleId is null) from the whole active MBS catalog, applying Fee = SourceFee *
    /// (1 + Percent/100) + FlatAmount. A plain copy is just Percent=0, FlatAmount=0.
    /// </summary>
    public async Task<int> ImportItemsAsync(int feeScheduleId, ImportFeeScheduleItemsRequest request)
    {
        Dictionary<int, decimal> sourceFees = request.SourceFeeScheduleId.HasValue
            ? await _context.FeeScheduleItems
                .Where(i => i.FeeScheduleId == request.SourceFeeScheduleId.Value)
                .ToDictionaryAsync(i => i.BillingItemId, i => i.Fee)
            : await _context.BillingItems
                .Where(b => b.IsActive)
                .ToDictionaryAsync(b => b.Id, b => b.ScheduleFee);

        // Round computed fees using the target schedule's rounding method (banker's rounding), matching legacy.
        var targetRounding = await _dbSet
            .Where(f => f.Id == feeScheduleId)
            .Select(f => f.RoundingType)
            .FirstOrDefaultAsync();

        var existing = await _context.FeeScheduleItems
            .Where(i => i.FeeScheduleId == feeScheduleId)
            .ToDictionaryAsync(i => i.BillingItemId);

        var now = DateTime.UtcNow;
        var affected = 0;
        foreach (var (billingItemId, sourceFee) in sourceFees)
        {
            var computedFee = BillingMath.ApplyScheduleRounding(sourceFee * (1 + request.Percent / 100m) + request.FlatAmount, targetRounding);

            if (existing.TryGetValue(billingItemId, out var item))
            {
                if (item.Fee != computedFee)
                {
                    item.Fee = computedFee;
                    item.UpdatedAt = now;
                    affected++;
                }
            }
            else
            {
                await _context.FeeScheduleItems.AddAsync(new FeeScheduleItem
                {
                    FeeScheduleId = feeScheduleId,
                    BillingItemId = billingItemId,
                    Fee = computedFee,
                    CreatedAt = now
                });
                affected++;
            }
        }

        if (affected > 0)
            await _context.SaveChangesAsync();

        return affected;
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

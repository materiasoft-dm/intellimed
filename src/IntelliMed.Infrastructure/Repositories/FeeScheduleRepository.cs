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

    /// <summary>Upsert: adding an item already on the schedule updates its fee instead of throwing on the unique (FeeScheduleId, BillingItemId) index, so retrying/double-clicking is safe.</summary>
    public async Task<int> AddItemAsync(int feeScheduleId, CreateFeeScheduleItemDto dto)
    {
        var existing = await _context.FeeScheduleItems
            .FirstOrDefaultAsync(i => i.FeeScheduleId == feeScheduleId && i.BillingItemId == dto.BillingItemId);

        if (existing != null)
        {
            if (existing.Fee != dto.Fee)
            {
                var now = DateTime.UtcNow;
                existing.Fee = dto.Fee;
                existing.UpdatedAt = now;
                AddHistoryEntry(existing, dto.Fee, now);
                await _context.SaveChangesAsync();
            }
            return existing.Id;
        }

        var created = DateTime.UtcNow;
        var item = new FeeScheduleItem
        {
            FeeScheduleId = feeScheduleId,
            BillingItemId = dto.BillingItemId,
            Fee = dto.Fee,
            CreatedAt = created
        };
        await _context.FeeScheduleItems.AddAsync(item);
        AddHistoryEntry(item, dto.Fee, created);
        await _context.SaveChangesAsync();
        return item.Id;
    }

    public async Task UpdateItemFeeAsync(int itemId, decimal fee)
    {
        var item = await _context.FeeScheduleItems.FindAsync(itemId);
        if (item == null)
            throw new InvalidOperationException($"FeeScheduleItem with ID {itemId} not found");

        if (item.Fee == fee) return;

        var now = DateTime.UtcNow;
        item.Fee = fee;
        item.UpdatedAt = now;
        AddHistoryEntry(item, fee, now);
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
                AddHistoryEntry(item, item.Fee, now);
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

        var rawFees = sourceFees.ToDictionary(
            kv => kv.Key,
            kv => kv.Value * (1 + request.Percent / 100m) + request.FlatAmount);

        return await UpsertItemsAsync(feeScheduleId, rawFees, targetRounding);
    }

    public async Task<int> ImportFromParsedFeesAsync(int feeScheduleId, IReadOnlyDictionary<string, decimal> feesByItemNumber)
    {
        var itemNumberToId = await _context.BillingItems
            .Where(b => feesByItemNumber.Keys.Contains(b.ItemNumber))
            .ToDictionaryAsync(b => b.ItemNumber, b => b.Id);

        var feesByBillingItemId = feesByItemNumber
            .Where(kv => itemNumberToId.ContainsKey(kv.Key))
            .ToDictionary(kv => itemNumberToId[kv.Key], kv => kv.Value);

        var rounding = await _dbSet
            .Where(f => f.Id == feeScheduleId)
            .Select(f => f.RoundingType)
            .FirstOrDefaultAsync();

        return await UpsertItemsAsync(feeScheduleId, feesByBillingItemId, rounding);
    }

    public async Task<SeedBulkBillResultDto> SeedBulkBillSchedulesAsync()
    {
        var definitions = new (string Code, string Description)[]
        {
            ("BBGP", "Bulk Bill - General Practice"),
            ("BBO", "Bulk Bill - Specialist (Rooms)"),
            ("BBI", "Bulk Bill - In-Hospital"),
        };

        var billingFees = await _context.BillingItems
            .Where(b => b.IsActive)
            .ToDictionaryAsync(b => b.Id, b => b.Benefit100 ?? b.ScheduleFee);

        var result = new SeedBulkBillResultDto();
        foreach (var (code, description) in definitions)
        {
            var schedule = await _dbSet.FirstOrDefaultAsync(f => f.Code == code);
            if (schedule == null)
            {
                schedule = new FeeSchedule
                {
                    Code = code,
                    Description = description,
                    RoundingType = RoundingTypeEnum.Exact,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbSet.AddAsync(schedule);
                await _context.SaveChangesAsync();
                result.SchedulesCreated++;
            }

            result.ItemsAffected += await UpsertItemsAsync(schedule.Id, billingFees, schedule.RoundingType);
        }

        return result;
    }

    /// <summary>
    /// Find-or-creates the VAGP/VASO/VASI DVA schedule shells. Unlike bulk-bill schedules, DVA has
    /// no authoritative fee source in our data (no MBS-derived proxy) — it's a distinct, published
    /// fee set with no public machine-readable feed, so this only creates empty schedules for an
    /// admin to populate via the generic CSV importer (SourceUrl/Fetch Now) or manual Add Item, once
    /// a DVA fee schedule has been exported to CSV from dva.gov.au. ItemsAffected is always 0 by design.
    /// </summary>
    public async Task<SeedBulkBillResultDto> SeedDvaScheduleShellsAsync()
    {
        var definitions = new (string Code, string Description)[]
        {
            ("VAGP", "DVA - General Practice"),
            ("VASO", "DVA - Specialist (Rooms)"),
            ("VASI", "DVA - In-Hospital"),
        };

        var result = new SeedBulkBillResultDto();
        foreach (var (code, description) in definitions)
        {
            var schedule = await _dbSet.FirstOrDefaultAsync(f => f.Code == code);
            if (schedule == null)
            {
                schedule = new FeeSchedule
                {
                    Code = code,
                    Description = description,
                    RoundingType = RoundingTypeEnum.Exact,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbSet.AddAsync(schedule);
                await _context.SaveChangesAsync();
                result.SchedulesCreated++;
            }
        }

        return result;
    }

    /// <summary>Shared upsert: writes a BillingItemId-&gt;fee map onto a schedule's FeeScheduleItems, rounded per the given RoundingType.</summary>
    private async Task<int> UpsertItemsAsync(int feeScheduleId, Dictionary<int, decimal> feesByBillingItemId, RoundingTypeEnum rounding)
    {
        var existing = await _context.FeeScheduleItems
            .Where(i => i.FeeScheduleId == feeScheduleId)
            .ToDictionaryAsync(i => i.BillingItemId);

        var now = DateTime.UtcNow;
        var affected = 0;
        foreach (var (billingItemId, rawFee) in feesByBillingItemId)
        {
            var fee = BillingMath.ApplyScheduleRounding(rawFee, rounding);

            if (existing.TryGetValue(billingItemId, out var item))
            {
                if (item.Fee != fee)
                {
                    item.Fee = fee;
                    item.UpdatedAt = now;
                    AddHistoryEntry(item, fee, now);
                    affected++;
                }
            }
            else
            {
                var newItem = new FeeScheduleItem
                {
                    FeeScheduleId = feeScheduleId,
                    BillingItemId = billingItemId,
                    Fee = fee,
                    CreatedAt = now
                };
                await _context.FeeScheduleItems.AddAsync(newItem);
                AddHistoryEntry(newItem, fee, now);
                affected++;
            }
        }

        if (affected > 0)
            await _context.SaveChangesAsync();

        return affected;
    }

    /// <summary>Tracks a new history row via the navigation property (not the FK id), so it resolves correctly even for an item that hasn't been saved yet in this same SaveChanges batch.</summary>
    private void AddHistoryEntry(FeeScheduleItem item, decimal fee, DateTime changedAt)
    {
        _context.FeeScheduleItemPriceHistories.Add(new FeeScheduleItemPriceHistory
        {
            FeeScheduleItem = item,
            Fee = fee,
            ChangedAt = changedAt
        });
    }

    public async Task<IEnumerable<FeeScheduleItemHistoryDto>> GetItemHistoryAsync(int feeScheduleItemId)
    {
        return await _context.FeeScheduleItemPriceHistories
            .Where(h => h.FeeScheduleItemId == feeScheduleItemId)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new FeeScheduleItemHistoryDto { Fee = h.Fee, ChangedAt = h.ChangedAt })
            .ToListAsync();
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

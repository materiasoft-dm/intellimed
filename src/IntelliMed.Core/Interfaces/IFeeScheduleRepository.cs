using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IFeeScheduleRepository : IRepository<FeeSchedule>
{
    new Task<FeeScheduleDto?> GetByIdAsync(int id);
    Task<IEnumerable<FeeScheduleDto>> GetAllActiveAsync();
    Task<(IEnumerable<FeeScheduleDto> Items, int TotalCount)> GetPagedAsync(FeeScheduleSearchDto search);
    Task<int> CreateAsync(CreateFeeScheduleDto dto);
    Task UpdateAsync(int id, UpdateFeeScheduleDto dto);
    Task ArchiveAsync(int id);

    Task<IEnumerable<FeeScheduleItemDto>> GetItemsAsync(int feeScheduleId);

    /// <summary>Page through a schedule's items ordered by item number, for infinite scroll.</summary>
    Task<(IEnumerable<FeeScheduleItemDto> Items, int TotalCount)> GetItemsPagedAsync(int feeScheduleId, int skip, int take);

    /// <summary>Re-fetch a single item (with its derived-item rates) after a mutation that only affects it,
    /// instead of reloading the whole (possibly large, paged) items list.</summary>
    Task<FeeScheduleItemDto?> GetItemByIdAsync(int itemId);

    Task<int> AddItemAsync(int feeScheduleId, CreateFeeScheduleItemDto dto);
    Task UpdateItemFeeAsync(int itemId, decimal fee);
    Task RemoveItemAsync(int itemId);
    Task UpdateItemOverridesAsync(int itemId, UpdateFeeScheduleItemOverridesDto dto);
    Task<int> AddPrecalculatedRateAsync(int feeScheduleItemId, SaveDerivedItemRateCalculatedDto dto);
    Task RemovePrecalculatedRateAsync(int id);
    Task<int> CopyFeesFromMbsAsync(int feeScheduleId);
    Task<int> ImportItemsAsync(int feeScheduleId, ImportFeeScheduleItemsRequest request);

    /// <summary>Upserts item fees on a schedule from an item-number-keyed fee map (e.g. parsed from a downloaded/uploaded file), rounded per the schedule's RoundingType.</summary>
    Task<int> ImportFromParsedFeesAsync(int feeScheduleId, IReadOnlyDictionary<string, decimal> feesByItemNumber);

    /// <summary>Find-or-creates the BBGP/BBO/BBI bulk-bill schedules and populates their items from the MBS catalog's Benefit100 (falling back to ScheduleFee).</summary>
    Task<SeedBulkBillResultDto> SeedBulkBillSchedulesAsync();

    /// <summary>Find-or-creates the VAGP/VASO/VASI DVA schedule shells (no fee auto-population — no authoritative source exists).</summary>
    Task<SeedBulkBillResultDto> SeedDvaScheduleShellsAsync();

    /// <summary>Find-or-creates the WorkCover QLD schedule shell (no fee auto-population — the fee list is a PDF, not a machine-readable feed).</summary>
    Task<SeedBulkBillResultDto> SeedWorkCoverQldScheduleShellAsync();

    /// <summary>Every fee this item has held, most recent first.</summary>
    Task<IEnumerable<FeeScheduleItemHistoryDto>> GetItemHistoryAsync(int feeScheduleItemId);
}

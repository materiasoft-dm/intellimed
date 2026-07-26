using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IFeeScheduleRepository : IRepository<FeeSchedule>
{
    Task<FeeScheduleDto?> GetByIdAsync(int id);
    Task<IEnumerable<FeeScheduleDto>> GetAllActiveAsync();
    Task<(IEnumerable<FeeScheduleDto> Items, int TotalCount)> GetPagedAsync(FeeScheduleSearchDto search);
    Task<int> CreateAsync(CreateFeeScheduleDto dto);
    Task UpdateAsync(int id, UpdateFeeScheduleDto dto);
    Task ArchiveAsync(int id);

    Task<IEnumerable<FeeScheduleItemDto>> GetItemsAsync(int feeScheduleId);
    Task<int> AddItemAsync(int feeScheduleId, CreateFeeScheduleItemDto dto);
    Task UpdateItemFeeAsync(int itemId, decimal fee);
    Task RemoveItemAsync(int itemId);
    Task<int> CopyFeesFromMbsAsync(int feeScheduleId);
    Task<int> ImportItemsAsync(int feeScheduleId, ImportFeeScheduleItemsRequest request);

    /// <summary>Upserts item fees on a schedule from an item-number-keyed fee map (e.g. parsed from a downloaded/uploaded file), rounded per the schedule's RoundingType.</summary>
    Task<int> ImportFromParsedFeesAsync(int feeScheduleId, IReadOnlyDictionary<string, decimal> feesByItemNumber);

    /// <summary>Find-or-creates the BBGP/BBO/BBI bulk-bill schedules and populates their items from the MBS catalog's Benefit100 (falling back to ScheduleFee).</summary>
    Task<SeedBulkBillResultDto> SeedBulkBillSchedulesAsync();

    /// <summary>Find-or-creates the VAGP/VASO/VASI DVA schedule shells (no fee auto-population — no authoritative source exists).</summary>
    Task<SeedBulkBillResultDto> SeedDvaScheduleShellsAsync();

    /// <summary>Every fee this item has held, most recent first.</summary>
    Task<IEnumerable<FeeScheduleItemHistoryDto>> GetItemHistoryAsync(int feeScheduleItemId);
}

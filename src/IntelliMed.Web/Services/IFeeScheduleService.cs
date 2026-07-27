using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IFeeScheduleService
{
    Task<PagedResult<FeeScheduleDto>> SearchFeeSchedulesAsync(FeeScheduleSearchDto search);
    Task<List<FeeScheduleDto>> GetAllActiveAsync();
    Task<FeeScheduleDto?> GetFeeScheduleByIdAsync(int id);
    Task<int?> CreateFeeScheduleAsync(CreateFeeScheduleDto dto);
    Task<bool> UpdateFeeScheduleAsync(int id, UpdateFeeScheduleDto dto);
    Task<bool> ArchiveFeeScheduleAsync(int id);
    Task<bool> DeleteFeeScheduleAsync(int id);

    Task<List<FeeScheduleItemDto>> GetItemsAsync(int feeScheduleId);
    Task<bool> AddItemAsync(int feeScheduleId, CreateFeeScheduleItemDto dto);
    Task<bool> UpdateItemFeeAsync(int itemId, decimal fee);
    Task<bool> RemoveItemAsync(int itemId);
    Task<bool> UpdateItemOverridesAsync(int itemId, UpdateFeeScheduleItemOverridesDto dto);
    Task<bool> AddPrecalculatedRateAsync(int itemId, SaveDerivedItemRateCalculatedDto dto);
    Task<bool> RemovePrecalculatedRateAsync(int id);
    Task<int?> CopyFeesFromMbsAsync(int feeScheduleId);
    Task<int?> ImportItemsAsync(int feeScheduleId, ImportFeeScheduleItemsRequest request);
    Task<SeedBulkBillResultDto?> SeedBulkBillSchedulesAsync();
    Task<SeedBulkBillResultDto?> SeedDvaScheduleShellsAsync();
    Task<FeeScheduleFetchResultDto?> FetchNowAsync(int feeScheduleId);
    Task<List<FeeScheduleItemHistoryDto>> GetItemHistoryAsync(int itemId);
}

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
}

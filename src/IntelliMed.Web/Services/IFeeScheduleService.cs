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
}

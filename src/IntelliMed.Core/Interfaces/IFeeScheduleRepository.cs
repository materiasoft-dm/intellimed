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
}

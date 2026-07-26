using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IDerivedItemConfigService
{
    Task<List<DerivedItemConfigDto>> GetAllAsync();
    Task<DerivedItemConfigDto?> GetByIdAsync(int id);
    Task<int?> CreateAsync(CreateDerivedItemConfigDto dto);
    Task<bool> UpdateAsync(int id, UpdateDerivedItemConfigDto dto);
    Task<bool> DeleteAsync(int id);
}

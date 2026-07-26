using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

public interface IDerivedItemConfigRepository
{
    Task<IEnumerable<DerivedItemConfigDto>> GetAllAsync();
    Task<DerivedItemConfigDto?> GetByIdAsync(int id);
    Task<int> CreateAsync(CreateDerivedItemConfigDto dto);
    Task UpdateAsync(int id, UpdateDerivedItemConfigDto dto);
    Task DeleteAsync(int id);
}

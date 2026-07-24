using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IProviderGroupRepository : IRepository<ProviderGroup>
{
    Task<ProviderGroupDto?> GetByIdAsync(int id);
    Task<IEnumerable<ProviderGroupDto>> GetAllActiveAsync();
}

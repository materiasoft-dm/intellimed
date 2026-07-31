using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface ISearchActionRepository : IRepository<SearchAction>
{
    Task<SearchActionDto?> GetByIdAsync(int id);
    Task<IEnumerable<SearchActionDto>> GetAllActiveAsync();
    Task<int> CreateAsync(SaveSearchActionRequest request);
    Task UpdateAsync(int id, SaveSearchActionRequest request);
    Task ArchiveAsync(int id);
}

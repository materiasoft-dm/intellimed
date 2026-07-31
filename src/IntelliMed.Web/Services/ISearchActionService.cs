using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface ISearchActionService
{
    Task<List<SearchActionDto>?> GetAllActiveAsync();
    Task<SearchActionDto?> GetByIdAsync(int id);
    Task<bool> CreateAsync(SaveSearchActionRequest request);
    Task<bool> UpdateAsync(int id, SaveSearchActionRequest request);
    Task<bool> ArchiveAsync(int id);
}

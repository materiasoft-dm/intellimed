using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IEpisodeService
{
    Task<List<EpisodeDto>> GetByClientIdAsync(int clientId, bool includeArchived = false);
    Task<int?> CreateAsync(CreateEpisodeDto dto);
    Task<bool> UpdateAsync(int clientId, int id, UpdateEpisodeDto dto);
    Task<bool> ResolveAsync(int clientId, int id);
    Task<bool> ReopenAsync(int clientId, int id);
    Task<bool> ArchiveAsync(int clientId, int id);
}

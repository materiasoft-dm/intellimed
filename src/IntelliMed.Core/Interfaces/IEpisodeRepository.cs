using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IEpisodeRepository : IRepository<Episode>
{
    new Task<EpisodeDto?> GetByIdAsync(int id);
    Task<IEnumerable<EpisodeDto>> GetByClientIdAsync(int clientId, bool includeArchived = false);
    Task<int> CreateAsync(CreateEpisodeDto dto);
    Task UpdateAsync(int id, UpdateEpisodeDto dto);
    Task ResolveAsync(int id);
    Task ReopenAsync(int id);
    Task ArchiveAsync(int id);
}

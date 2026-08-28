using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IClientHealthIssueRepository : IRepository<ClientHealthIssue>
{
    new Task<ClientHealthIssueDto?> GetByIdAsync(int id);

    /// <summary>Archived rows are excluded unless includeArchived is set. Ordered active-first then
    /// most-recently-created, so the current problem list reads at the top.</summary>
    Task<IEnumerable<ClientHealthIssueDto>> GetByClientIdAsync(int clientId, bool includeArchived = false);

    /// <summary>Only the issues flagged for the patient summary, active and unarchived — backs the
    /// compact list on the progress-note editor.</summary>
    Task<IEnumerable<ClientHealthIssueDto>> GetSummaryForClientAsync(int clientId);

    Task<int> CreateAsync(CreateClientHealthIssueDto dto);
    Task UpdateAsync(int id, UpdateClientHealthIssueDto dto);
    Task ArchiveAsync(int id);
}

using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IClientHealthIssueService
{
    Task<List<ClientHealthIssueDto>> GetByClientIdAsync(int clientId, bool includeArchived = false);

    /// <summary>Active, summary-flagged issues only — backs the compact problem list shown on the
    /// progress-note editor.</summary>
    Task<List<ClientHealthIssueDto>> GetSummaryAsync(int clientId);

    Task<bool> CreateAsync(CreateClientHealthIssueDto dto);
    Task<bool> UpdateAsync(int clientId, int id, UpdateClientHealthIssueDto dto);
    Task<bool> ArchiveAsync(int clientId, int id);
}

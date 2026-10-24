using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IResultRepository
{
    Task<PagedResult<ResultSummaryDto>> SearchAsync(ResultSearchDto search);
    Task<ResultDetailDto?> GetByIdAsync(int id);
    Task<ResultCountsDto> GetCountsAsync(int? clinicId, int? practitionerId);
    Task<ResultAttachment?> GetAttachmentAsync(int reportId, int attachmentId);

    /// <summary>Which clinic a report belongs to (for access checks), or null if it does not exist.</summary>
    Task<int?> GetClinicIdAsync(int id);

    /// <summary>File an Unmatched (or mis-matched) report under a client and move it to the inbox.</summary>
    Task<bool> MatchAsync(int id, int clientId, bool rememberLabIdentifier, string userId);

    /// <summary>Acknowledge → Archived, recording who and when.</summary>
    Task<bool> AcknowledgeAsync(int id, string? comment, string userId);

    /// <summary>Park for later (stays unacknowledged).</summary>
    Task<bool> HoldAsync(int id, string? note);

    /// <summary>Held or Archived back to the inbox.</summary>
    Task<bool> ReopenAsync(int id);

    Task<List<ResultIngestKeyDto>> GetIngestKeysAsync(int? clinicId);
    Task<CreatedResultIngestKeyDto> CreateIngestKeyAsync(int clinicId, string label);
    Task<bool> RevokeIngestKeyAsync(int id);

    /// <summary>Resolve a presented key to its clinic and stamp LastUsedAt. Null if unknown or revoked.</summary>
    Task<int?> ResolveIngestKeyAsync(string key);
}

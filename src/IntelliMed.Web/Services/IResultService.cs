using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Web.Services;

public interface IResultService
{
    Task<PagedResult<ResultSummaryDto>> SearchAsync(ResultStatus? status, string? query, bool abnormalOnly, bool mine, int page, int pageSize, int? clientId = null);
    Task<ResultCountsDto> GetCountsAsync(bool mine);
    Task<ResultDetailDto?> GetAsync(int id);

    /// <summary>Returns the outcome, or an error message the user can act on.</summary>
    Task<(IngestResultOutcomeDto? Outcome, string? Error)> UploadAsync(Stream content, string fileName);

    Task<bool> MatchAsync(int id, int clientId, bool rememberLabIdentifier);
    Task<bool> AcknowledgeAsync(int id, string? comment);
    Task<bool> HoldAsync(int id, string? note);
    Task<bool> ReopenAsync(int id);

    /// <summary>The attachment's bytes, for saving to disk.</summary>
    Task<byte[]?> GetAttachmentAsync(int reportId, int attachmentId);

    Task<List<ResultIngestKeyDto>> GetIngestKeysAsync();
    Task<CreatedResultIngestKeyDto?> CreateIngestKeyAsync(string label);
    Task<bool> RevokeIngestKeyAsync(int id);
}

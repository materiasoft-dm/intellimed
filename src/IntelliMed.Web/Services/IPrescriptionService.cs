using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

/// <summary>Wraps both PrescriptionsController (client-scoped) and PrescriptionIssuesController
/// (flat, for the print page) — one service per feature, not one per API controller.</summary>
public interface IPrescriptionService
{
    Task<List<PrescriptionDto>> GetByClientIdAsync(int clientId, bool includeInactive = false);
    Task<MedicationSafetyCheckResultDto?> CheckSafetyAsync(int clientId, CheckPrescriptionSafetyDto dto);
    Task<int?> CreateAsync(CreatePrescriptionDto dto);
    Task<bool> UpdateAsync(int clientId, int id, UpdatePrescriptionDto dto);
    Task<bool> CeaseAsync(int clientId, int id);
    Task<bool> CancelAsync(int clientId, int id);
    Task<List<PrescriptionIssueDto>> GetIssuesAsync(int clientId, int id);
    Task<int?> IssueAsync(int clientId, int id, IssuePrescriptionDto dto);

    /// <summary>Flat lookup by issue id, backing PrescriptionPrint.razor.</summary>
    Task<PrescriptionIssueDto?> GetIssueByIdAsync(int issueId);
}

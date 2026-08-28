using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

/// <summary>No Update/Delete — a PrescriptionIssue is an immutable audit record of one actual
/// issue/print/reprint event. See PrescriptionIssue.cs.</summary>
public interface IPrescriptionIssueRepository : IRepository<PrescriptionIssue>
{
    new Task<PrescriptionIssueDto?> GetByIdAsync(int id);
    Task<IEnumerable<PrescriptionIssueDto>> GetByPrescriptionIdAsync(int prescriptionId);
    Task<int> IssueAsync(int prescriptionId, IssuePrescriptionDto dto);
}

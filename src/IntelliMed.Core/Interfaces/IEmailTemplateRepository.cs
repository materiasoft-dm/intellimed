using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IEmailTemplateRepository : IRepository<EmailTemplate>
{
    Task<List<EmailTemplateDto>> GetAllAsync(int clinicId);
    Task<EmailTemplateDto?> GetByIdAsync(int id);

    /// <summary>The single non-archived template currently assigned to the given event, if any — used by the invite/forgot-password senders.</summary>
    Task<EmailTemplate?> GetActiveByEventKeyAsync(int clinicId, string eventKey);

    Task<int> CreateAsync(int clinicId, SaveEmailTemplateRequest request);
    Task UpdateAsync(int id, SaveEmailTemplateRequest request);
    Task ArchiveAsync(int id);
}

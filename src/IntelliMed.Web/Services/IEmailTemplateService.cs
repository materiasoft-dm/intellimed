using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IEmailTemplateService
{
    Task<List<EmailTemplateDto>?> GetAllAsync();
    Task<EmailTemplateDto?> GetByIdAsync(int id);
    Task<bool> CreateAsync(SaveEmailTemplateRequest request);
    Task<bool> UpdateAsync(int id, SaveEmailTemplateRequest request);
    Task<bool> ArchiveAsync(int id);
}

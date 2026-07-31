using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class EmailTemplateRepository : Repository<EmailTemplate>, IEmailTemplateRepository
{
    public EmailTemplateRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<EmailTemplateDto>> GetAllAsync(int clinicId)
    {
        var templates = await _dbSet
            .Where(t => t.ClinicId == clinicId)
            .OrderBy(t => t.IsArchived)
            .ThenBy(t => t.Name)
            .ToListAsync();
        return templates.Select(ToDto).ToList();
    }

    public async Task<EmailTemplateDto?> GetByIdAsync(int id)
    {
        var template = await _dbSet.FirstOrDefaultAsync(t => t.Id == id);
        return template == null ? null : ToDto(template);
    }

    public async Task<EmailTemplate?> GetActiveByEventKeyAsync(int clinicId, string eventKey)
    {
        return await _dbSet.FirstOrDefaultAsync(t =>
            t.ClinicId == clinicId && t.EventKey == eventKey && !t.IsArchived);
    }

    public async Task<int> CreateAsync(int clinicId, SaveEmailTemplateRequest request)
    {
        if (!string.IsNullOrEmpty(request.EventKey))
            await UnassignEventKeyAsync(clinicId, request.EventKey, excludeId: null);

        var template = new EmailTemplate
        {
            ClinicId = clinicId,
            Name = request.Name,
            Subject = request.Subject,
            BodyHtml = request.BodyHtml,
            EventKey = string.IsNullOrEmpty(request.EventKey) ? null : request.EventKey,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbSet.AddAsync(template);
        await _context.SaveChangesAsync();
        return template.Id;
    }

    public async Task UpdateAsync(int id, SaveEmailTemplateRequest request)
    {
        var template = await _dbSet.FindAsync(id);
        if (template == null)
            throw new InvalidOperationException($"EmailTemplate with ID {id} not found");

        if (!string.IsNullOrEmpty(request.EventKey))
            await UnassignEventKeyAsync(template.ClinicId, request.EventKey, excludeId: id);

        template.Name = request.Name;
        template.Subject = request.Subject;
        template.BodyHtml = request.BodyHtml;
        template.EventKey = string.IsNullOrEmpty(request.EventKey) ? null : request.EventKey;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var template = await _dbSet.FindAsync(id);
        if (template == null)
            throw new InvalidOperationException($"EmailTemplate with ID {id} not found");

        template.IsArchived = true;
        template.EventKey = null;
        template.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    /// <summary>Only one active template can hold a given event key per clinic — assigning it here unassigns whoever held it before.</summary>
    private async Task UnassignEventKeyAsync(int clinicId, string eventKey, int? excludeId)
    {
        var holders = await _dbSet
            .Where(t => t.ClinicId == clinicId && t.EventKey == eventKey && t.Id != (excludeId ?? -1))
            .ToListAsync();

        foreach (var holder in holders)
            holder.EventKey = null;
    }

    private static EmailTemplateDto ToDto(EmailTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Subject = t.Subject,
        BodyHtml = t.BodyHtml,
        EventKey = t.EventKey,
        IsArchived = t.IsArchived,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}

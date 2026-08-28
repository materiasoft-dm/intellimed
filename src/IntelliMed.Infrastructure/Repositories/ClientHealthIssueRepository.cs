using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ClientHealthIssueRepository : Repository<ClientHealthIssue>, IClientHealthIssueRepository
{
    public ClientHealthIssueRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<ClientHealthIssueDto?> GetByIdAsync(int id)
    {
        var issue = await _dbSet.FirstOrDefaultAsync(h => h.Id == id);
        return issue == null ? null : EntityMapper.ToDto(issue);
    }

    public async Task<IEnumerable<ClientHealthIssueDto>> GetByClientIdAsync(int clientId, bool includeArchived = false)
    {
        var query = _dbSet.Where(h => h.ClientId == clientId);
        if (!includeArchived)
            query = query.Where(h => !h.IsArchived);

        var issues = await query
            .OrderByDescending(h => h.IsActive)
            .ThenByDescending(h => h.CreatedAt)
            .ToListAsync();
        return issues.Select(EntityMapper.ToDto);
    }

    public async Task<IEnumerable<ClientHealthIssueDto>> GetSummaryForClientAsync(int clientId)
    {
        var issues = await _dbSet
            .Where(h => h.ClientId == clientId && h.IsSummary && h.IsActive && !h.IsArchived)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
        return issues.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateClientHealthIssueDto dto)
    {
        var issue = new ClientHealthIssue
        {
            ClientId = dto.ClientId,
            Condition = dto.Condition,
            Code = dto.Code,
            CodingSystem = dto.CodingSystem,
            Side = dto.Side,
            OnsetYear = dto.OnsetYear,
            OnsetMonth = dto.OnsetMonth,
            IsActive = dto.IsActive,
            IsSummary = dto.IsSummary,
            IsConfidential = dto.IsConfidential,
            Note = dto.Note,
            CreatedAt = DateTime.UtcNow
        };

        await _dbSet.AddAsync(issue);
        await _context.SaveChangesAsync();
        return issue.Id;
    }

    public async Task UpdateAsync(int id, UpdateClientHealthIssueDto dto)
    {
        var issue = await _dbSet.FindAsync(id);
        if (issue == null)
            throw new InvalidOperationException($"ClientHealthIssue with ID {id} not found");

        issue.Condition = dto.Condition;
        issue.Code = dto.Code;
        issue.CodingSystem = dto.CodingSystem;
        issue.Side = dto.Side;
        issue.OnsetYear = dto.OnsetYear;
        issue.OnsetMonth = dto.OnsetMonth;
        issue.IsActive = dto.IsActive;
        issue.IsSummary = dto.IsSummary;
        issue.IsConfidential = dto.IsConfidential;
        issue.Note = dto.Note;
        issue.IsArchived = dto.IsArchived;
        issue.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var issue = await _dbSet.FindAsync(id);
        if (issue == null)
            throw new InvalidOperationException($"ClientHealthIssue with ID {id} not found");

        issue.IsArchived = true;
        issue.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

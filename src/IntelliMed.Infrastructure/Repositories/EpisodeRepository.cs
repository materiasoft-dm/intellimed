using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class EpisodeRepository : Repository<Episode>, IEpisodeRepository
{
    public EpisodeRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<EpisodeDto?> GetByIdAsync(int id)
    {
        var episode = await _dbSet.FirstOrDefaultAsync(e => e.Id == id);
        return episode == null ? null : EntityMapper.ToDto(episode);
    }

    public async Task<IEnumerable<EpisodeDto>> GetByClientIdAsync(int clientId, bool includeArchived = false)
    {
        var query = _dbSet.Where(e => e.ClientId == clientId);
        if (!includeArchived)
            query = query.Where(e => !e.IsArchived);

        var episodes = await query.OrderByDescending(e => e.OpenedDate).ToListAsync();
        return episodes.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateEpisodeDto dto)
    {
        var episode = new Episode
        {
            ClientId = dto.ClientId,
            Title = dto.Title,
            Description = dto.Description,
            Status = EpisodeStatus.Active,
            OpenedDate = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow
        };

        await _dbSet.AddAsync(episode);
        await _context.SaveChangesAsync();
        return episode.Id;
    }

    public async Task UpdateAsync(int id, UpdateEpisodeDto dto)
    {
        var episode = await _dbSet.FirstOrDefaultAsync(e => e.Id == id);
        if (episode == null)
            throw new InvalidOperationException($"Episode with ID {id} not found");

        episode.Title = dto.Title;
        episode.Description = dto.Description;
        episode.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task ResolveAsync(int id)
    {
        var episode = await _dbSet.FirstOrDefaultAsync(e => e.Id == id);
        if (episode == null)
            throw new InvalidOperationException($"Episode with ID {id} not found");

        episode.Status = EpisodeStatus.Resolved;
        episode.ClosedDate = DateTime.UtcNow.Date;
        episode.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task ReopenAsync(int id)
    {
        var episode = await _dbSet.FirstOrDefaultAsync(e => e.Id == id);
        if (episode == null)
            throw new InvalidOperationException($"Episode with ID {id} not found");

        episode.Status = EpisodeStatus.Active;
        episode.ClosedDate = null;
        episode.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var episode = await _dbSet.FirstOrDefaultAsync(e => e.Id == id);
        if (episode == null)
            throw new InvalidOperationException($"Episode with ID {id} not found");

        episode.IsArchived = true;
        episode.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

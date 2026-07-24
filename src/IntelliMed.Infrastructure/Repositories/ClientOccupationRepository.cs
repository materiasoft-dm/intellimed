using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ClientOccupationRepository : Repository<ClientOccupation>, IClientOccupationRepository
{
    public ClientOccupationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ClientOccupationDto?> GetByIdAsync(int id)
    {
        var occupation = await _dbSet.FindAsync(id);
        return occupation == null ? null : EntityMapper.ToDto(occupation);
    }

    public async Task<IEnumerable<ClientOccupationDto>> GetByClientIdAsync(int clientId)
    {
        var occupations = await _dbSet
            .Where(o => o.ClientId == clientId)
            .OrderByDescending(o => o.StartedYear)
            .ToListAsync();
        return occupations.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateClientOccupationDto dto)
    {
        var occupation = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(occupation);
        await _context.SaveChangesAsync();
        return occupation.Id;
    }

    public async Task UpdateAsync(int id, UpdateClientOccupationDto dto)
    {
        var occupation = await _dbSet.FindAsync(id);
        if (occupation == null)
            throw new InvalidOperationException($"ClientOccupation with ID {id} not found");

        EntityMapper.UpdateEntity(occupation, dto);
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var occupation = await _dbSet.FindAsync(id);
        if (occupation == null)
            throw new InvalidOperationException($"ClientOccupation with ID {id} not found");

        occupation.IsArchived = true;
        occupation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

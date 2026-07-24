using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ClientCompensationClaimRepository : Repository<ClientCompensationClaim>, IClientCompensationClaimRepository
{
    public ClientCompensationClaimRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ClientCompensationClaimDto?> GetByIdAsync(int id)
    {
        var claim = await _dbSet.FindAsync(id);
        return claim == null ? null : EntityMapper.ToDto(claim);
    }

    public async Task<IEnumerable<ClientCompensationClaimDto>> GetByClientIdAsync(int clientId)
    {
        var claims = await _dbSet
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return claims.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateClientCompensationClaimDto dto)
    {
        var claim = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(claim);
        await _context.SaveChangesAsync();
        return claim.Id;
    }

    public async Task UpdateAsync(int id, UpdateClientCompensationClaimDto dto)
    {
        var claim = await _dbSet.FindAsync(id);
        if (claim == null)
            throw new InvalidOperationException($"ClientCompensationClaim with ID {id} not found");

        EntityMapper.UpdateEntity(claim, dto);
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var claim = await _dbSet.FindAsync(id);
        if (claim == null)
            throw new InvalidOperationException($"ClientCompensationClaim with ID {id} not found");

        claim.IsArchived = true;
        claim.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

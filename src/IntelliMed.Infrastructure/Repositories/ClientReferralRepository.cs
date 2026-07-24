using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ClientReferralRepository : Repository<ClientReferral>, IClientReferralRepository
{
    public ClientReferralRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ClientReferralDto?> GetByIdAsync(int id)
    {
        var referral = await _dbSet.FindAsync(id);
        return referral == null ? null : EntityMapper.ToDto(referral);
    }

    public async Task<IEnumerable<ClientReferralDto>> GetByClientIdAsync(int clientId)
    {
        var referrals = await _dbSet
            .Where(r => r.ClientId == clientId)
            .OrderByDescending(r => r.ReferralDate)
            .ToListAsync();
        return referrals.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateClientReferralDto dto)
    {
        var referral = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(referral);
        await _context.SaveChangesAsync();
        return referral.Id;
    }

    public async Task UpdateAsync(int id, UpdateClientReferralDto dto)
    {
        var referral = await _dbSet.FindAsync(id);
        if (referral == null)
            throw new InvalidOperationException($"ClientReferral with ID {id} not found");

        EntityMapper.UpdateEntity(referral, dto);
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var referral = await _dbSet.FindAsync(id);
        if (referral == null)
            throw new InvalidOperationException($"ClientReferral with ID {id} not found");

        referral.IsArchived = true;
        referral.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

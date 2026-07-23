using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class PatientReferralRepository : Repository<PatientReferral>, IPatientReferralRepository
{
    public PatientReferralRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PatientReferralDto?> GetByIdAsync(int id)
    {
        var referral = await _dbSet.FindAsync(id);
        return referral == null ? null : EntityMapper.ToDto(referral);
    }

    public async Task<IEnumerable<PatientReferralDto>> GetByPatientIdAsync(int patientId)
    {
        var referrals = await _dbSet
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.ReferralDate)
            .ToListAsync();
        return referrals.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreatePatientReferralDto dto)
    {
        var referral = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(referral);
        await _context.SaveChangesAsync();
        return referral.Id;
    }

    public async Task UpdateAsync(int id, UpdatePatientReferralDto dto)
    {
        var referral = await _dbSet.FindAsync(id);
        if (referral == null)
            throw new InvalidOperationException($"PatientReferral with ID {id} not found");

        EntityMapper.UpdateEntity(referral, dto);
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var referral = await _dbSet.FindAsync(id);
        if (referral == null)
            throw new InvalidOperationException($"PatientReferral with ID {id} not found");

        referral.IsArchived = true;
        referral.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

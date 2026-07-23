using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class PatientOccupationRepository : Repository<PatientOccupation>, IPatientOccupationRepository
{
    public PatientOccupationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PatientOccupationDto?> GetByIdAsync(int id)
    {
        var occupation = await _dbSet.FindAsync(id);
        return occupation == null ? null : EntityMapper.ToDto(occupation);
    }

    public async Task<IEnumerable<PatientOccupationDto>> GetByPatientIdAsync(int patientId)
    {
        var occupations = await _dbSet
            .Where(o => o.PatientId == patientId)
            .OrderByDescending(o => o.StartedYear)
            .ToListAsync();
        return occupations.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreatePatientOccupationDto dto)
    {
        var occupation = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(occupation);
        await _context.SaveChangesAsync();
        return occupation.Id;
    }

    public async Task UpdateAsync(int id, UpdatePatientOccupationDto dto)
    {
        var occupation = await _dbSet.FindAsync(id);
        if (occupation == null)
            throw new InvalidOperationException($"PatientOccupation with ID {id} not found");

        EntityMapper.UpdateEntity(occupation, dto);
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var occupation = await _dbSet.FindAsync(id);
        if (occupation == null)
            throw new InvalidOperationException($"PatientOccupation with ID {id} not found");

        occupation.IsArchived = true;
        occupation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

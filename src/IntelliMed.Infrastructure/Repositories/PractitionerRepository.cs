using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class PractitionerRepository : Repository<Practitioner>, IPractitionerRepository
{
    public PractitionerRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PractitionerDto?> GetByIdAsync(int id)
    {
        var practitioner = await _dbSet.FindAsync(id);
        return practitioner == null ? null : EntityMapper.ToDto(practitioner);
    }

    public async Task<IEnumerable<PractitionerDto>> GetAllActiveAsync()
    {
        var practitioners = await _dbSet
            .Where(p => p.IsActive)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();
        return practitioners.Select(EntityMapper.ToDto);
    }

    public async Task<IEnumerable<PractitionerDto>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            var allPractitioners = await _dbSet
                .Where(p => p.IsActive)
                .OrderBy(p => p.LastName)
                .ToListAsync();
            return allPractitioners.Select(EntityMapper.ToDto);
        }

        var searchTerm = query.ToLower();
        var practitioners = await _dbSet
            .Where(p => p.IsActive && (
                p.FirstName.ToLower().Contains(searchTerm) ||
                p.LastName.ToLower().Contains(searchTerm) ||
                p.Profession.ToLower().Contains(searchTerm) ||
                (p.ProviderNumber != null && p.ProviderNumber.Contains(query))))
            .OrderBy(p => p.LastName)
            .ToListAsync();
        return practitioners.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreatePractitionerDto dto)
    {
        var practitioner = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(practitioner);
        await _context.SaveChangesAsync();
        return practitioner.Id;
    }

    public async Task UpdateAsync(int id, UpdatePractitionerDto dto)
    {
        var practitioner = await _dbSet.FindAsync(id);
        if (practitioner == null)
            throw new InvalidOperationException($"Practitioner with ID {id} not found");

        EntityMapper.UpdateEntity(practitioner, dto);
        await _context.SaveChangesAsync();
    }
}
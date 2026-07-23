using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class PatientRepository : Repository<Patient>, IPatientRepository
{
    public PatientRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PatientDto?> GetByIdAsync(int id)
    {
        var patient = await _dbSet.FindAsync(id);
        return patient == null ? null : EntityMapper.ToDto(patient);
    }

    public async Task<IEnumerable<PatientDto>> SearchAsync(PatientSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var patients = await query.ToListAsync();
        return patients.Select(EntityMapper.ToDto);
    }

    public async Task<(IEnumerable<PatientDto> Items, int TotalCount)> GetPagedAsync(PatientSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var totalCount = await query.CountAsync();
        
        var patients = await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync();

        return (patients.Select(EntityMapper.ToDto), totalCount);
    }

    public async Task<int> CreateAsync(CreatePatientDto dto)
    {
        var patient = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(patient);
        await _context.SaveChangesAsync();
        return patient.Id;
    }

    public async Task UpdateAsync(int id, UpdatePatientDto dto)
    {
        var patient = await _dbSet.FindAsync(id);
        if (patient == null)
            throw new InvalidOperationException($"Patient with ID {id} not found");

        EntityMapper.UpdateEntity(patient, dto);
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var patient = await _dbSet.FindAsync(id);
        if (patient == null)
            throw new InvalidOperationException($"Patient with ID {id} not found");

        patient.IsActive = false;
        patient.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    private IQueryable<Patient> BuildSearchQuery(PatientSearchDto search)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var searchTerm = search.Query.ToLower();
            query = query.Where(p =>
                p.FirstName.ToLower().Contains(searchTerm) ||
                p.LastName.ToLower().Contains(searchTerm) ||
                (p.Email != null && p.Email.ToLower().Contains(searchTerm)) ||
                (p.Phone != null && p.Phone.Contains(searchTerm)) ||
                (p.MedicareNumber != null && p.MedicareNumber.Contains(searchTerm)));
        }

        if (search.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == search.IsActive.Value);
        }

        return query;
    }
}
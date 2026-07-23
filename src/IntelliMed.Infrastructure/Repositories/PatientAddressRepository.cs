using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class PatientAddressRepository : Repository<PatientAddress>, IPatientAddressRepository
{
    public PatientAddressRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PatientAddressDto?> GetByIdAsync(int id)
    {
        var address = await _dbSet.FindAsync(id);
        return address == null ? null : EntityMapper.ToDto(address);
    }

    public async Task<IEnumerable<PatientAddressDto>> GetByPatientIdAsync(int patientId)
    {
        var addresses = await _dbSet
            .Where(a => a.PatientId == patientId)
            .ToListAsync();
        return addresses.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreatePatientAddressDto dto)
    {
        var address = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(address);
        await _context.SaveChangesAsync();
        return address.Id;
    }

    public async Task UpdateAsync(int id, UpdatePatientAddressDto dto)
    {
        var address = await _dbSet.FindAsync(id);
        if (address == null)
            throw new InvalidOperationException($"PatientAddress with ID {id} not found");

        EntityMapper.UpdateEntity(address, dto);
        await _context.SaveChangesAsync();
    }
}

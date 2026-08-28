using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ClientAllergyRepository : Repository<ClientAllergy>, IClientAllergyRepository
{
    public ClientAllergyRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<ClientAllergyDto?> GetByIdAsync(int id)
    {
        var allergy = await _dbSet.Include(a => a.ActiveIngredient).FirstOrDefaultAsync(a => a.Id == id);
        return allergy == null ? null : EntityMapper.ToDto(allergy);
    }

    public async Task<IEnumerable<ClientAllergyDto>> GetByClientIdAsync(int clientId)
    {
        var allergies = await _dbSet
            .Include(a => a.ActiveIngredient)
            .Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return allergies.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateClientAllergyDto dto)
    {
        var allergy = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(allergy);
        await _context.SaveChangesAsync();
        return allergy.Id;
    }

    public async Task UpdateAsync(int id, UpdateClientAllergyDto dto)
    {
        var allergy = await _dbSet.FindAsync(id);
        if (allergy == null)
            throw new InvalidOperationException($"ClientAllergy with ID {id} not found");

        EntityMapper.UpdateEntity(allergy, dto);
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var allergy = await _dbSet.FindAsync(id);
        if (allergy == null)
            throw new InvalidOperationException($"ClientAllergy with ID {id} not found");

        allergy.IsArchived = true;
        allergy.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

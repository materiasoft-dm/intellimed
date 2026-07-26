using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class DerivedItemConfigRepository : IDerivedItemConfigRepository
{
    private readonly AppDbContext _context;

    public DerivedItemConfigRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<DerivedItemConfigDto>> GetAllAsync()
    {
        var configs = await _context.DerivedItemConfigs
            .Include(c => c.BillingItem)
            .Include(c => c.AssociatedBillingItem)
            .OrderBy(c => c.BillingItem!.ItemNumber)
            .ToListAsync();
        return configs.Select(EntityMapper.ToDto);
    }

    public async Task<DerivedItemConfigDto?> GetByIdAsync(int id)
    {
        var config = await _context.DerivedItemConfigs
            .Include(c => c.BillingItem)
            .Include(c => c.AssociatedBillingItem)
            .FirstOrDefaultAsync(c => c.Id == id);
        return config == null ? null : EntityMapper.ToDto(config);
    }

    public async Task<int> CreateAsync(CreateDerivedItemConfigDto dto)
    {
        var config = EntityMapper.ToEntity(dto);
        await _context.DerivedItemConfigs.AddAsync(config);
        await _context.SaveChangesAsync();
        return config.Id;
    }

    public async Task UpdateAsync(int id, UpdateDerivedItemConfigDto dto)
    {
        var config = await _context.DerivedItemConfigs.FindAsync(id);
        if (config == null)
            throw new InvalidOperationException($"DerivedItemConfig with ID {id} not found");

        EntityMapper.UpdateEntity(config, dto);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var config = await _context.DerivedItemConfigs.FindAsync(id);
        if (config != null)
        {
            _context.DerivedItemConfigs.Remove(config);
            await _context.SaveChangesAsync();
        }
    }
}

using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class BillingItemRepository : Repository<BillingItem>, IBillingItemRepository
{
    public BillingItemRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<BillingItemDto?> GetByIdAsync(int id)
    {
        var item = await _dbSet.FindAsync(id);
        return item == null ? null : EntityMapper.ToDto(item);
    }

    public async Task<IEnumerable<BillingItemDto>> SearchAsync(string query, int take = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            var recent = await _dbSet
                .Where(b => b.IsActive)
                .OrderBy(b => b.ItemNumber)
                .Take(take)
                .ToListAsync();
            return recent.Select(EntityMapper.ToDto);
        }

        var searchTerm = query.ToLower();
        var items = await _dbSet
            .Where(b => b.IsActive && (
                b.ItemNumber.ToLower().StartsWith(searchTerm) ||
                b.Description.ToLower().Contains(searchTerm)))
            .OrderBy(b => b.ItemNumber)
            .Take(take)
            .ToListAsync();
        return items.Select(EntityMapper.ToDto);
    }
}

using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ProviderGroupRepository : Repository<ProviderGroup>, IProviderGroupRepository
{
    public ProviderGroupRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ProviderGroupDto?> GetByIdAsync(int id)
    {
        var group = await _dbSet.FindAsync(id);
        return group == null ? null : ToDto(group);
    }

    public async Task<IEnumerable<ProviderGroupDto>> GetAllActiveAsync()
    {
        var groups = await _dbSet
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .ToListAsync();
        return groups.Select(ToDto);
    }

    private static ProviderGroupDto ToDto(ProviderGroup group) => new()
    {
        Id = group.Id,
        Name = group.Name
    };
}

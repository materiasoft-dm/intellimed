using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class HealthFundRepository : Repository<HealthFund>, IHealthFundRepository
{
    public HealthFundRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<HealthFundDto?> GetByIdAsync(int id)
    {
        var fund = await _dbSet.FindAsync(id);
        return fund == null ? null : ToDto(fund);
    }

    public async Task<IEnumerable<HealthFundDto>> GetAllActiveAsync()
    {
        var funds = await _dbSet
            .Where(f => f.IsActive)
            .OrderBy(f => f.Name)
            .ToListAsync();
        return funds.Select(ToDto);
    }

    private static HealthFundDto ToDto(HealthFund fund) => new()
    {
        Id = fund.Id,
        Code = fund.Code,
        Name = fund.Name
    };
}

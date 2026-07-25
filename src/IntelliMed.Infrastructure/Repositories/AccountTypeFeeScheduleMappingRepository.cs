using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class AccountTypeFeeScheduleMappingRepository : IAccountTypeFeeScheduleMappingRepository
{
    private readonly AppDbContext _context;

    public AccountTypeFeeScheduleMappingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<AccountTypeFeeScheduleMappingDto>> GetMappingsAsync(int clinicId)
    {
        var mappings = await _context.AccountTypeFeeScheduleMappings
            .Include(m => m.FeeSchedule)
            .Where(m => m.ClinicId == clinicId)
            .ToListAsync();

        // Return a row for every account type so the UI can show all of them.
        return Enum.GetValues<AccountTypeEnum>().Select(accountType =>
        {
            var mapping = mappings.FirstOrDefault(m => m.AccountType == accountType);
            return new AccountTypeFeeScheduleMappingDto
            {
                AccountType = accountType,
                FeeScheduleId = mapping?.FeeScheduleId,
                FeeScheduleCode = mapping?.FeeSchedule?.Code
            };
        }).ToList();
    }

    public async Task SaveMappingAsync(int clinicId, SaveAccountTypeFeeScheduleMappingDto dto)
    {
        var existing = await _context.AccountTypeFeeScheduleMappings
            .FirstOrDefaultAsync(m => m.ClinicId == clinicId && m.AccountType == dto.AccountType);

        if (dto.FeeScheduleId == null)
        {
            // Clearing the mapping.
            if (existing != null)
            {
                _context.AccountTypeFeeScheduleMappings.Remove(existing);
                await _context.SaveChangesAsync();
            }
            return;
        }

        if (existing == null)
        {
            await _context.AccountTypeFeeScheduleMappings.AddAsync(new AccountTypeFeeScheduleMapping
            {
                ClinicId = clinicId,
                AccountType = dto.AccountType,
                FeeScheduleId = dto.FeeScheduleId.Value,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.FeeScheduleId = dto.FeeScheduleId.Value;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}

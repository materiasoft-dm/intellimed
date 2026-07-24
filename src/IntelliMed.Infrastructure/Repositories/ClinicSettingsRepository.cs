using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ClinicSettingsRepository : Repository<ClinicSettings>, IClinicSettingsRepository
{
    public ClinicSettingsRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ClinicSettingsDto> GetSingletonAsync()
    {
        var settings = await _dbSet.SingleAsync(s => s.Id == 1);
        return new ClinicSettingsDto
        {
            PracticeName = settings.PracticeName,
            Abn = settings.Abn,
            Phone = settings.Phone,
            Fax = settings.Fax,
            Email = settings.Email,
            Website = settings.Website,
            Address = settings.Address,
            Suburb = settings.Suburb,
            Postcode = settings.Postcode,
            State = settings.State
        };
    }

    public async Task UpdateSingletonAsync(UpdateClinicSettingsRequest request)
    {
        var settings = await _dbSet.SingleAsync(s => s.Id == 1);
        settings.PracticeName = request.PracticeName;
        settings.Abn = request.Abn;
        settings.Phone = request.Phone;
        settings.Fax = request.Fax;
        settings.Email = request.Email;
        settings.Website = request.Website;
        settings.Address = request.Address;
        settings.Suburb = request.Suburb;
        settings.Postcode = request.Postcode;
        settings.State = request.State;
        await _context.SaveChangesAsync();
    }
}

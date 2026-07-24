using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ClinicRepository : Repository<Clinic>, IClinicRepository
{
    public ClinicRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ClinicDto>> GetAllAsync()
    {
        var clinics = await _dbSet
            .OrderBy(c => c.Name)
            .Select(c => new ClinicDto
            {
                Id = c.Id,
                Name = c.Name,
                Abn = c.Abn,
                Phone = c.Phone,
                Fax = c.Fax,
                Email = c.Email,
                Address = c.Address,
                Suburb = c.Suburb,
                Postcode = c.Postcode,
                State = c.State,
                IsActive = c.IsActive,
                UserCount = c.UserClinics.Count
            })
            .ToListAsync();

        return clinics;
    }

    public async Task<ClinicDto?> GetDtoByIdAsync(int id)
    {
        var clinic = await _dbSet
            .Where(c => c.Id == id)
            .Select(c => new ClinicDto
            {
                Id = c.Id,
                Name = c.Name,
                Abn = c.Abn,
                Phone = c.Phone,
                Fax = c.Fax,
                Email = c.Email,
                Address = c.Address,
                Suburb = c.Suburb,
                Postcode = c.Postcode,
                State = c.State,
                IsActive = c.IsActive,
                UserCount = c.UserClinics.Count
            })
            .SingleOrDefaultAsync();

        return clinic;
    }

    public async Task<int> CreateAsync(CreateClinicDto dto)
    {
        var clinic = new Clinic
        {
            Name = dto.Name,
            Abn = dto.Abn,
            Phone = dto.Phone,
            Fax = dto.Fax,
            Email = dto.Email,
            Address = dto.Address,
            Suburb = dto.Suburb,
            Postcode = dto.Postcode,
            State = dto.State
        };

        await _dbSet.AddAsync(clinic);
        await _context.SaveChangesAsync();
        return clinic.Id;
    }

    public async Task UpdateAsync(int id, UpdateClinicDto dto)
    {
        var clinic = await _dbSet.FindAsync(id);
        if (clinic == null)
            throw new InvalidOperationException($"Clinic with ID {id} not found");

        clinic.Name = dto.Name;
        clinic.Abn = dto.Abn;
        clinic.Phone = dto.Phone;
        clinic.Fax = dto.Fax;
        clinic.Email = dto.Email;
        clinic.Address = dto.Address;
        clinic.Suburb = dto.Suburb;
        clinic.Postcode = dto.Postcode;
        clinic.State = dto.State;
        clinic.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<MyClinicDto>> GetMyClinicsAsync(string applicationUserId)
    {
        return await _context.UserClinics
            .Where(uc => uc.ApplicationUserId == applicationUserId && uc.Clinic!.IsActive)
            .OrderBy(uc => uc.Clinic!.Name)
            .Select(uc => new MyClinicDto { Id = uc.ClinicId, Name = uc.Clinic!.Name })
            .ToListAsync();
    }

    public async Task<IEnumerable<MyClinicDto>> GetLookupAsync()
    {
        return await _dbSet
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new MyClinicDto { Id = c.Id, Name = c.Name })
            .ToListAsync();
    }

    public async Task<IEnumerable<ClinicUserDto>> GetClinicUsersAsync(int clinicId)
    {
        var assignedUserIds = await _context.UserClinics
            .Where(uc => uc.ClinicId == clinicId)
            .Select(uc => uc.ApplicationUserId)
            .ToListAsync();

        var users = await _context.Users
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new ClinicUserDto
            {
                Id = u.Id,
                FullName = u.FirstName + " " + u.LastName,
                Email = u.Email ?? string.Empty,
                IsAssigned = assignedUserIds.Contains(u.Id)
            })
            .ToListAsync();

        return users;
    }

    public async Task SetClinicUsersAsync(int clinicId, IList<string> userIds)
    {
        var existing = await _context.UserClinics
            .Where(uc => uc.ClinicId == clinicId)
            .ToListAsync();

        _context.UserClinics.RemoveRange(existing);

        var newAssignments = userIds.Distinct().Select(userId => new UserClinic
        {
            ClinicId = clinicId,
            ApplicationUserId = userId
        });

        await _context.UserClinics.AddRangeAsync(newAssignments);
        await _context.SaveChangesAsync();
    }

    public async Task SetUserClinicsAsync(string applicationUserId, IList<int> clinicIds)
    {
        var existing = await _context.UserClinics
            .Where(uc => uc.ApplicationUserId == applicationUserId)
            .ToListAsync();

        _context.UserClinics.RemoveRange(existing);

        var newAssignments = clinicIds.Distinct().Select(clinicId => new UserClinic
        {
            ApplicationUserId = applicationUserId,
            ClinicId = clinicId
        });

        await _context.UserClinics.AddRangeAsync(newAssignments);
        await _context.SaveChangesAsync();
    }
}

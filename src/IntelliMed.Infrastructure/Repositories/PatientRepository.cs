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
        var patient = await _dbSet.Include(p => p.HealthFund).FirstOrDefaultAsync(p => p.Id == id);
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
        var query = _dbSet.Include(p => p.HealthFund).AsQueryable();

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

        // Basic
        if (!string.IsNullOrWhiteSpace(search.Surname))
        {
            var term = search.Surname.ToLower();
            query = query.Where(p => p.LastName.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(search.GivenName))
        {
            var term = search.GivenName.ToLower();
            query = query.Where(p => p.FirstName.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(search.MedicareNumber))
        {
            query = query.Where(p => p.MedicareNumber.Contains(search.MedicareNumber));
        }
        if (search.Gender.HasValue)
        {
            query = query.Where(p => p.Gender == search.Gender.Value);
        }
        if (!string.IsNullOrWhiteSpace(search.DvaNumber))
        {
            query = query.Where(p => p.DvaNumber != null && p.DvaNumber.Contains(search.DvaNumber));
        }
        if (!string.IsNullOrWhiteSpace(search.FileNumber))
        {
            query = query.Where(p => p.FileNumber != null && p.FileNumber.Contains(search.FileNumber));
        }
        if (!string.IsNullOrWhiteSpace(search.PensionNumber))
        {
            query = query.Where(p => p.PensionNumber != null && p.PensionNumber.Contains(search.PensionNumber));
        }
        if (!string.IsNullOrWhiteSpace(search.HealthFundNumber))
        {
            query = query.Where(p => p.HealthFundNumber != null && p.HealthFundNumber.Contains(search.HealthFundNumber));
        }
        if (!string.IsNullOrWhiteSpace(search.LifeCardNum))
        {
            query = query.Where(p => p.LifeCardNum != null && p.LifeCardNum.Contains(search.LifeCardNum));
        }
        if (search.DobFrom.HasValue)
        {
            query = query.Where(p => p.DateOfBirth >= search.DobFrom.Value);
        }
        if (search.DobTo.HasValue)
        {
            query = query.Where(p => p.DateOfBirth <= search.DobTo.Value);
        }

        // Residential address
        if (!string.IsNullOrWhiteSpace(search.Address))
        {
            var term = search.Address.ToLower();
            query = query.Where(p => p.Address.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(search.Suburb))
        {
            var term = search.Suburb.ToLower();
            query = query.Where(p => p.Suburb.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(search.Postcode))
        {
            query = query.Where(p => p.Postcode.Contains(search.Postcode));
        }
        if (!string.IsNullOrWhiteSpace(search.State))
        {
            query = query.Where(p => p.State.ToLower() == search.State.ToLower());
        }

        // Postal address — matched against PatientAddress rows of type Postal
        if (!string.IsNullOrWhiteSpace(search.PostalAddress))
        {
            var term = search.PostalAddress.ToLower();
            query = query.Where(p => _context.PatientAddresses.Any(a =>
                a.PatientId == p.Id && a.AddressType == PatientAddressType.Postal &&
                a.AddressLine1.ToLower().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(search.PostalSuburb))
        {
            var term = search.PostalSuburb.ToLower();
            query = query.Where(p => _context.PatientAddresses.Any(a =>
                a.PatientId == p.Id && a.AddressType == PatientAddressType.Postal &&
                a.Suburb.ToLower().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(search.PostalPostcode))
        {
            query = query.Where(p => _context.PatientAddresses.Any(a =>
                a.PatientId == p.Id && a.AddressType == PatientAddressType.Postal &&
                a.Postcode.Contains(search.PostalPostcode)));
        }
        if (!string.IsNullOrWhiteSpace(search.PostalState))
        {
            var term = search.PostalState.ToLower();
            query = query.Where(p => _context.PatientAddresses.Any(a =>
                a.PatientId == p.Id && a.AddressType == PatientAddressType.Postal &&
                a.State.ToLower() == term));
        }

        // Contact
        if (!string.IsNullOrWhiteSpace(search.HomePhone))
        {
            query = query.Where(p => p.Phone.Contains(search.HomePhone));
        }
        if (!string.IsNullOrWhiteSpace(search.BusinessHoursPhone))
        {
            query = query.Where(p => p.BusinessHoursPhone != null && p.BusinessHoursPhone.Contains(search.BusinessHoursPhone));
        }
        if (!string.IsNullOrWhiteSpace(search.MobilePhone))
        {
            query = query.Where(p => p.MobilePhone != null && p.MobilePhone.Contains(search.MobilePhone));
        }
        if (!string.IsNullOrWhiteSpace(search.Email))
        {
            var term = search.Email.ToLower();
            query = query.Where(p => p.Email.ToLower().Contains(term));
        }
        if (search.AtsiStatus.HasValue)
        {
            query = query.Where(p => p.AtsiStatus == search.AtsiStatus.Value);
        }

        // Date ranges
        if (search.CreatedFrom.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= search.CreatedFrom.Value);
        }
        if (search.CreatedTo.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= search.CreatedTo.Value);
        }
        if (search.MedicareExpiryFrom.HasValue)
        {
            query = query.Where(p => p.MedicareExpiryDate != null && p.MedicareExpiryDate >= search.MedicareExpiryFrom.Value);
        }
        if (search.MedicareExpiryTo.HasValue)
        {
            query = query.Where(p => p.MedicareExpiryDate != null && p.MedicareExpiryDate <= search.MedicareExpiryTo.Value);
        }
        if (search.HealthFundJoinFrom.HasValue)
        {
            query = query.Where(p => p.HealthFundJoinDate != null && p.HealthFundJoinDate >= search.HealthFundJoinFrom.Value);
        }
        if (search.HealthFundJoinTo.HasValue)
        {
            query = query.Where(p => p.HealthFundJoinDate != null && p.HealthFundJoinDate <= search.HealthFundJoinTo.Value);
        }

        // Misc / account
        if (!string.IsNullOrWhiteSpace(search.Warnings))
        {
            var term = search.Warnings.ToLower();
            query = query.Where(p => p.Warnings != null && p.Warnings.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(search.Notes))
        {
            var term = search.Notes.ToLower();
            query = query.Where(p => p.Notes != null && p.Notes.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(search.ReferredBy))
        {
            var term = search.ReferredBy.ToLower();
            query = query.Where(p => _context.PatientReferrals.Any(r =>
                r.PatientId == p.Id && r.ReferringProviderName.ToLower().Contains(term)));
        }
        if (search.PatientType.HasValue)
        {
            query = query.Where(p => p.Type == search.PatientType.Value);
        }
        if (!string.IsNullOrWhiteSpace(search.UrNumber))
        {
            query = query.Where(p => p.UrNumber != null && p.UrNumber.Contains(search.UrNumber));
        }
        if (search.HealthFundId.HasValue)
        {
            query = query.Where(p => p.HealthFundId == search.HealthFundId.Value);
        }
        if (search.PayerPatientId.HasValue)
        {
            query = query.Where(p => p.PayerPatientId == search.PayerPatientId.Value);
        }
        if (search.AccountTypes is { Count: > 0 })
        {
            query = query.Where(p => search.AccountTypes.Contains(p.AccountType));
        }

        // Flags
        if (search.Deceased.HasValue)
        {
            query = query.Where(p => p.Deceased == search.Deceased.Value);
        }
        if (!search.IncludeArchived)
        {
            query = query.Where(p => p.IsActive);
        }
        if (search.AcceptEmail.HasValue)
        {
            query = query.Where(p => p.AcceptEmail == search.AcceptEmail.Value);
        }
        if (search.AcceptSms.HasValue)
        {
            query = query.Where(p => p.AcceptSms == search.AcceptSms.Value);
        }
        if (search.AcceptSmsMarketing.HasValue)
        {
            query = query.Where(p => p.AcceptSmsMarketing == search.AcceptSmsMarketing.Value);
        }

        if (search.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == search.IsActive.Value);
        }

        return query;
    }
}

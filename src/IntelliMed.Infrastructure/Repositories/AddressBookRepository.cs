using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class AddressBookRepository : Repository<AddressBookContact>, IAddressBookRepository
{
    public AddressBookRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<AddressBookContactDto?> GetByIdAsync(int id)
    {
        var contact = await _dbSet.Include(c => c.Clinic).FirstOrDefaultAsync(c => c.Id == id);
        return contact == null ? null : EntityMapper.ToDto(contact);
    }

    public async Task<PagedResult<AddressBookContactDto>> SearchAsync(AddressBookSearchDto search)
    {
        var query = _dbSet.Include(c => c.Clinic).AsQueryable();

        if (!search.IncludeArchived)
            query = query.Where(c => !c.IsArchived);

        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var term = search.Query.Trim().ToLower();
            query = query.Where(c =>
                c.LastName.ToLower().Contains(term) ||
                (c.FirstName != null && c.FirstName.ToLower().Contains(term)) ||
                (c.ProviderNumber != null && c.ProviderNumber.ToLower().Contains(term)) ||
                (c.Profession != null && c.Profession.ToLower().Contains(term)) ||
                (c.Speciality != null && c.Speciality.ToLower().Contains(term)) ||
                (c.Clinic != null && c.Clinic.Name.ToLower().Contains(term)));
        }

        var page = Math.Max(search.Page, 1);
        var pageSize = Math.Clamp(search.PageSize, 1, 100);
        var totalCount = await query.CountAsync();
        var contacts = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<AddressBookContactDto>
        {
            Items = contacts.Select(EntityMapper.ToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<int> CreateAsync(SaveAddressBookContactDto dto)
    {
        var contact = new AddressBookContact();
        EntityMapper.UpdateEntity(contact, dto);
        contact.CreatedAt = DateTime.UtcNow;
        contact.UpdatedAt = null;
        await _dbSet.AddAsync(contact);
        await _context.SaveChangesAsync();
        return contact.Id;
    }

    public async Task UpdateAsync(int id, SaveAddressBookContactDto dto)
    {
        var contact = await _dbSet.FindAsync(id)
            ?? throw new InvalidOperationException($"AddressBookContact with ID {id} not found");
        EntityMapper.UpdateEntity(contact, dto);
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var contact = await _dbSet.FindAsync(id)
            ?? throw new InvalidOperationException($"AddressBookContact with ID {id} not found");
        contact.IsArchived = true;
        contact.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

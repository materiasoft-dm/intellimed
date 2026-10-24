using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IAddressBookRepository : IRepository<AddressBookContact>
{
    new Task<AddressBookContactDto?> GetByIdAsync(int id);
    Task<PagedResult<AddressBookContactDto>> SearchAsync(AddressBookSearchDto search);
    Task<int> CreateAsync(SaveAddressBookContactDto dto);
    Task UpdateAsync(int id, SaveAddressBookContactDto dto);
    Task ArchiveAsync(int id);
}

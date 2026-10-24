using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IAddressBookService
{
    Task<PagedResult<AddressBookContactDto>> SearchAsync(AddressBookSearchDto search);
    Task<AddressBookContactDto?> GetByIdAsync(int id);
    Task<int?> CreateAsync(SaveAddressBookContactDto dto);
    Task<bool> UpdateAsync(int id, SaveAddressBookContactDto dto);
    Task<bool> ArchiveAsync(int id);
}

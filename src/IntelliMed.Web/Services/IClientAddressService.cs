using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IClientAddressService
{
    Task<IEnumerable<ClientAddressDto>> GetByClientIdAsync(int clientId);
    Task<bool> CreateAsync(CreateClientAddressDto dto);
    Task<bool> UpdateAsync(int clientId, int id, UpdateClientAddressDto dto);
    Task<bool> DeleteAsync(int clientId, int id);
}

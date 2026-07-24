using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IClientAddressRepository : IRepository<ClientAddress>
{
    Task<ClientAddressDto?> GetByIdAsync(int id);
    Task<IEnumerable<ClientAddressDto>> GetByClientIdAsync(int clientId);
    Task<int> CreateAsync(CreateClientAddressDto dto);
    Task UpdateAsync(int id, UpdateClientAddressDto dto);
}

using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IClientRepository : IRepository<Client>
{
    Task<ClientDto?> GetByIdAsync(int id);
    Task<IEnumerable<ClientDto>> SearchAsync(ClientSearchDto search);
    Task<(IEnumerable<ClientDto> Items, int TotalCount)> GetPagedAsync(ClientSearchDto search);
    Task<int> CreateAsync(CreateClientDto dto);
    Task UpdateAsync(int id, UpdateClientDto dto);
    Task ArchiveAsync(int id);
}

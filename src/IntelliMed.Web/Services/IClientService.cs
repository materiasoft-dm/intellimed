using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IClientService
{
    Task<PagedResult<ClientDto>> SearchClientsAsync(ClientSearchDto search);
    Task<ClientDto?> GetClientByIdAsync(int id);
    Task<int?> CreateClientAsync(CreateClientDto dto);
    Task<bool> UpdateClientAsync(int id, UpdateClientDto dto);
    Task<bool> ArchiveClientAsync(int id);
}

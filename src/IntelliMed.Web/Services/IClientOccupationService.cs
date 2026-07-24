using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IClientOccupationService
{
    Task<IEnumerable<ClientOccupationDto>> GetByClientIdAsync(int clientId);
    Task<bool> CreateAsync(CreateClientOccupationDto dto);
    Task<bool> UpdateAsync(int clientId, int id, UpdateClientOccupationDto dto);
    Task<bool> ArchiveAsync(int clientId, int id);
}

using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IClientOccupationRepository : IRepository<ClientOccupation>
{
    Task<ClientOccupationDto?> GetByIdAsync(int id);
    Task<IEnumerable<ClientOccupationDto>> GetByClientIdAsync(int clientId);
    Task<int> CreateAsync(CreateClientOccupationDto dto);
    Task UpdateAsync(int id, UpdateClientOccupationDto dto);
    Task ArchiveAsync(int id);
}

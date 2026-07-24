using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IClientCompensationClaimRepository : IRepository<ClientCompensationClaim>
{
    Task<ClientCompensationClaimDto?> GetByIdAsync(int id);
    Task<IEnumerable<ClientCompensationClaimDto>> GetByClientIdAsync(int clientId);
    Task<int> CreateAsync(CreateClientCompensationClaimDto dto);
    Task UpdateAsync(int id, UpdateClientCompensationClaimDto dto);
    Task ArchiveAsync(int id);
}

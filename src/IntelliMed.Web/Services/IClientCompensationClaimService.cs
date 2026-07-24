using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IClientCompensationClaimService
{
    Task<IEnumerable<ClientCompensationClaimDto>> GetByClientIdAsync(int clientId);
    Task<bool> CreateAsync(CreateClientCompensationClaimDto dto);
    Task<bool> UpdateAsync(int clientId, int id, UpdateClientCompensationClaimDto dto);
    Task<bool> ArchiveAsync(int clientId, int id);
}

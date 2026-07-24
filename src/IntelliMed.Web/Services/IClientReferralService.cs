using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IClientReferralService
{
    Task<IEnumerable<ClientReferralDto>> GetByClientIdAsync(int clientId);
    Task<bool> CreateAsync(CreateClientReferralDto dto);
    Task<bool> UpdateAsync(int clientId, int id, UpdateClientReferralDto dto);
    Task<bool> ArchiveAsync(int clientId, int id);
    Task<bool> DeleteAsync(int clientId, int id);
}

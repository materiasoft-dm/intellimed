using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IClientReferralRepository : IRepository<ClientReferral>
{
    Task<ClientReferralDto?> GetByIdAsync(int id);
    Task<IEnumerable<ClientReferralDto>> GetByClientIdAsync(int clientId);
    Task<int> CreateAsync(CreateClientReferralDto dto);
    Task UpdateAsync(int id, UpdateClientReferralDto dto);
    Task ArchiveAsync(int id);
}

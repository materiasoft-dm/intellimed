using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IClientFamilyRelationshipRepository : IRepository<ClientFamilyRelationship>
{
    Task<IEnumerable<ClientFamilyRelationshipDto>> GetByClientIdAsync(int clientId);
    Task<int> CreateAsync(CreateClientFamilyRelationshipDto dto);
}

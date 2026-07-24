using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IClientFamilyService
{
    Task<IEnumerable<ClientFamilyRelationshipDto>> GetByClientIdAsync(int clientId);
    Task<bool> CreateAsync(CreateClientFamilyRelationshipDto dto);
    Task<bool> DeleteAsync(int clientId, int id);
}

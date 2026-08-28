using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IClientAllergyService
{
    Task<IEnumerable<ClientAllergyDto>> GetByClientIdAsync(int clientId);
    Task<bool> CreateAsync(CreateClientAllergyDto dto);
    Task<bool> UpdateAsync(int clientId, int id, UpdateClientAllergyDto dto);
    Task<bool> ArchiveAsync(int clientId, int id);
    Task<bool> DeleteAsync(int clientId, int id);
}

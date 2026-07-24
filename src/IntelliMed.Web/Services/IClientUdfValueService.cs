using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IClientUdfValueService
{
    Task<IEnumerable<ClientUserDefinedFieldValueDto>> GetByClientIdAsync(int clientId);
    Task<bool> CreateAsync(CreateClientUserDefinedFieldValueDto dto);
    Task<bool> UpdateAsync(int clientId, int id, UpdateClientUserDefinedFieldValueDto dto);
    Task<bool> DeleteAsync(int clientId, int id);
}

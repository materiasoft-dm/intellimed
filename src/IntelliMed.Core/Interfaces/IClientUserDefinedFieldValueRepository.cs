using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IClientUserDefinedFieldValueRepository : IRepository<ClientUserDefinedFieldValue>
{
    Task<ClientUserDefinedFieldValueDto?> GetByIdAsync(int id);
    Task<IEnumerable<ClientUserDefinedFieldValueDto>> GetByClientIdAsync(int clientId);
    Task<int> CreateAsync(CreateClientUserDefinedFieldValueDto dto);
    Task UpdateAsync(int id, UpdateClientUserDefinedFieldValueDto dto);
}

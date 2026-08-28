using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IClientAllergyRepository : IRepository<ClientAllergy>
{
    new Task<ClientAllergyDto?> GetByIdAsync(int id);
    Task<IEnumerable<ClientAllergyDto>> GetByClientIdAsync(int clientId);
    Task<int> CreateAsync(CreateClientAllergyDto dto);
    Task UpdateAsync(int id, UpdateClientAllergyDto dto);
    Task ArchiveAsync(int id);
}

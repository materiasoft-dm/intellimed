using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IPractitionerRepository : IRepository<Practitioner>
{
    Task<PractitionerDto?> GetByIdAsync(int id);
    Task<IEnumerable<PractitionerDto>> GetAllActiveAsync();
    Task<IEnumerable<PractitionerDto>> SearchAsync(string query);
    Task<int> CreateAsync(CreatePractitionerDto dto);
    Task UpdateAsync(int id, UpdatePractitionerDto dto);
}
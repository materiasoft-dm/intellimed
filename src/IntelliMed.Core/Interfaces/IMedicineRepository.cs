using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IMedicineRepository : IRepository<Medicine>
{
    Task<MedicineDto?> GetByIdAsync(int id);
    Task<IEnumerable<MedicineDto>> SearchAsync(string query, MedicineSource? source = null, int take = 20);
    Task<int> CreateAsync(CreateMedicineDto dto);
    Task UpdateAsync(int id, UpdateMedicineDto dto);
    Task DeactivateAsync(int id);
}

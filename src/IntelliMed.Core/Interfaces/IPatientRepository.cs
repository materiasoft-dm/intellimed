using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IPatientRepository : IRepository<Patient>
{
    Task<PatientDto?> GetByIdAsync(int id);
    Task<IEnumerable<PatientDto>> SearchAsync(PatientSearchDto search);
    Task<(IEnumerable<PatientDto> Items, int TotalCount)> GetPagedAsync(PatientSearchDto search);
    Task<int> CreateAsync(CreatePatientDto dto);
    Task UpdateAsync(int id, UpdatePatientDto dto);
}
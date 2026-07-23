using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IPatientUserDefinedFieldValueRepository : IRepository<PatientUserDefinedFieldValue>
{
    Task<PatientUserDefinedFieldValueDto?> GetByIdAsync(int id);
    Task<IEnumerable<PatientUserDefinedFieldValueDto>> GetByPatientIdAsync(int patientId);
    Task<int> CreateAsync(CreatePatientUserDefinedFieldValueDto dto);
    Task UpdateAsync(int id, UpdatePatientUserDefinedFieldValueDto dto);
}

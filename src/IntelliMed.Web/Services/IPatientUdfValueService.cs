using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IPatientUdfValueService
{
    Task<IEnumerable<PatientUserDefinedFieldValueDto>> GetByPatientIdAsync(int patientId);
    Task<bool> CreateAsync(CreatePatientUserDefinedFieldValueDto dto);
    Task<bool> UpdateAsync(int patientId, int id, UpdatePatientUserDefinedFieldValueDto dto);
    Task<bool> DeleteAsync(int patientId, int id);
}

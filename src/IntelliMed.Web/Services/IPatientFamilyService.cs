using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IPatientFamilyService
{
    Task<IEnumerable<PatientFamilyRelationshipDto>> GetByPatientIdAsync(int patientId);
    Task<bool> CreateAsync(CreatePatientFamilyRelationshipDto dto);
    Task<bool> DeleteAsync(int patientId, int id);
}

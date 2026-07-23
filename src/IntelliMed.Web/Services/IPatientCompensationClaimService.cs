using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IPatientCompensationClaimService
{
    Task<IEnumerable<PatientCompensationClaimDto>> GetByPatientIdAsync(int patientId);
    Task<bool> CreateAsync(CreatePatientCompensationClaimDto dto);
    Task<bool> UpdateAsync(int patientId, int id, UpdatePatientCompensationClaimDto dto);
    Task<bool> ArchiveAsync(int patientId, int id);
}

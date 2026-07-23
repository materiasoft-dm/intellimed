using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IPatientReferralService
{
    Task<IEnumerable<PatientReferralDto>> GetByPatientIdAsync(int patientId);
    Task<bool> CreateAsync(CreatePatientReferralDto dto);
    Task<bool> UpdateAsync(int patientId, int id, UpdatePatientReferralDto dto);
    Task<bool> ArchiveAsync(int patientId, int id);
    Task<bool> DeleteAsync(int patientId, int id);
}

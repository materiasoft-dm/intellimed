using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IPatientReferralRepository : IRepository<PatientReferral>
{
    Task<PatientReferralDto?> GetByIdAsync(int id);
    Task<IEnumerable<PatientReferralDto>> GetByPatientIdAsync(int patientId);
    Task<int> CreateAsync(CreatePatientReferralDto dto);
    Task UpdateAsync(int id, UpdatePatientReferralDto dto);
    Task ArchiveAsync(int id);
}

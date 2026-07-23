using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IPatientCompensationClaimRepository : IRepository<PatientCompensationClaim>
{
    Task<PatientCompensationClaimDto?> GetByIdAsync(int id);
    Task<IEnumerable<PatientCompensationClaimDto>> GetByPatientIdAsync(int patientId);
    Task<int> CreateAsync(CreatePatientCompensationClaimDto dto);
    Task UpdateAsync(int id, UpdatePatientCompensationClaimDto dto);
    Task ArchiveAsync(int id);
}

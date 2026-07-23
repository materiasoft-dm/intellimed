using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IPatientFamilyRelationshipRepository : IRepository<PatientFamilyRelationship>
{
    Task<IEnumerable<PatientFamilyRelationshipDto>> GetByPatientIdAsync(int patientId);
    Task<int> CreateAsync(CreatePatientFamilyRelationshipDto dto);
}

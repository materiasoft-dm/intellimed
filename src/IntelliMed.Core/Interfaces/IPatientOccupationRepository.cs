using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IPatientOccupationRepository : IRepository<PatientOccupation>
{
    Task<PatientOccupationDto?> GetByIdAsync(int id);
    Task<IEnumerable<PatientOccupationDto>> GetByPatientIdAsync(int patientId);
    Task<int> CreateAsync(CreatePatientOccupationDto dto);
    Task UpdateAsync(int id, UpdatePatientOccupationDto dto);
    Task ArchiveAsync(int id);
}

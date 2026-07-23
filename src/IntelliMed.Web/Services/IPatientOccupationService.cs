using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IPatientOccupationService
{
    Task<IEnumerable<PatientOccupationDto>> GetByPatientIdAsync(int patientId);
    Task<bool> CreateAsync(CreatePatientOccupationDto dto);
    Task<bool> UpdateAsync(int patientId, int id, UpdatePatientOccupationDto dto);
    Task<bool> ArchiveAsync(int patientId, int id);
}

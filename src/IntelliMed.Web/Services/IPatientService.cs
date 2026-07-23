using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IPatientService
{
    Task<PagedResult<PatientDto>> SearchPatientsAsync(PatientSearchDto search);
    Task<PatientDto?> GetPatientByIdAsync(int id);
    Task<int?> CreatePatientAsync(CreatePatientDto dto);
    Task<bool> UpdatePatientAsync(int id, UpdatePatientDto dto);
    Task<bool> ArchivePatientAsync(int id);
}
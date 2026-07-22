using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IPatientService
{
    Task<PagedResult<PatientDto>> SearchPatientsAsync(PatientSearchDto search);
    Task<bool> CreatePatientAsync(CreatePatientDto dto);
}
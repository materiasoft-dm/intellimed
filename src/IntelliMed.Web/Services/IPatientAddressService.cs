using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IPatientAddressService
{
    Task<IEnumerable<PatientAddressDto>> GetByPatientIdAsync(int patientId);
    Task<bool> CreateAsync(CreatePatientAddressDto dto);
    Task<bool> UpdateAsync(int patientId, int id, UpdatePatientAddressDto dto);
    Task<bool> DeleteAsync(int patientId, int id);
}

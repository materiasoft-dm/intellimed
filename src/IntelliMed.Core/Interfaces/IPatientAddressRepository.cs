using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IPatientAddressRepository : IRepository<PatientAddress>
{
    Task<PatientAddressDto?> GetByIdAsync(int id);
    Task<IEnumerable<PatientAddressDto>> GetByPatientIdAsync(int patientId);
    Task<int> CreateAsync(CreatePatientAddressDto dto);
    Task UpdateAsync(int id, UpdatePatientAddressDto dto);
}

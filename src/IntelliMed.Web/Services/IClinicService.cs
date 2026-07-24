using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IClinicService
{
    Task<List<MyClinicDto>?> GetMyClinicsAsync();
    Task<List<MyClinicDto>?> GetLookupAsync();
    Task<List<ClinicDto>?> GetAllAsync();
    Task<ClinicDto?> GetByIdAsync(int id);
    Task<bool> CreateAsync(CreateClinicDto dto);
    Task<bool> UpdateAsync(int id, UpdateClinicDto dto);
    Task<List<ClinicUserDto>?> GetClinicUsersAsync(int id);
    Task<bool> SetClinicUsersAsync(int id, SetClinicUsersRequest request);
}

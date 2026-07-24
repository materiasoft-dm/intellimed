using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IClinicRepository : IRepository<Clinic>
{
    Task<IEnumerable<ClinicDto>> GetAllAsync();
    Task<ClinicDto?> GetDtoByIdAsync(int id);
    Task<int> CreateAsync(CreateClinicDto dto);
    Task UpdateAsync(int id, UpdateClinicDto dto);
    Task<IEnumerable<MyClinicDto>> GetMyClinicsAsync(string applicationUserId);
    Task<IEnumerable<MyClinicDto>> GetLookupAsync();
    Task<IEnumerable<ClinicUserDto>> GetClinicUsersAsync(int clinicId);
    Task SetClinicUsersAsync(int clinicId, IList<string> userIds);
    Task SetUserClinicsAsync(string applicationUserId, IList<int> clinicIds);
}

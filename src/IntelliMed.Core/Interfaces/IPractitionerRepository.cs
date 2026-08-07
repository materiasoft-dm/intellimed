using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IPractitionerRepository : IRepository<Practitioner>
{
    Task<PractitionerDto?> GetByIdAsync(int id);
    Task<IEnumerable<PractitionerDto>> GetAllActiveAsync();
    Task<IEnumerable<PractitionerDto>> SearchAsync(string query);
    Task<int> CreateAsync(CreatePractitionerDto dto);
    Task UpdateAsync(int id, UpdatePractitionerDto dto);

    /// <summary>The practitioner (if any) linked to this login — see Practitioner.ApplicationUserId.
    /// Returns the raw entity, not a DTO, since callers need to either read its Id (patient
    /// visibility filtering) or mutate ApplicationUserId (admin re-linking).</summary>
    Task<Practitioner?> GetByApplicationUserIdAsync(string userId);

    /// <summary>Sets Practitioner.ApplicationUserId, first clearing it off whichever practitioner
    /// (if any) currently holds it — a login can only be linked to one practitioner at a time.</summary>
    Task SetLinkedUserAsync(int practitionerId, string? applicationUserId);
}
using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

public interface IAccountTypeFeeScheduleMappingRepository
{
    /// <summary>Returns one row per account type for the clinic, with its current mapping (if any).</summary>
    Task<IEnumerable<AccountTypeFeeScheduleMappingDto>> GetMappingsAsync(int clinicId);

    /// <summary>Upserts (or clears, when FeeScheduleId is null) the mapping for one account type.</summary>
    Task SaveMappingAsync(int clinicId, SaveAccountTypeFeeScheduleMappingDto dto);
}

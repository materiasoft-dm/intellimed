using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

public interface IProviderScheduleRepository
{
    Task<List<ProviderScheduleDayDto>> GetByUserIdAsync(string applicationUserId);
    Task SetScheduleAsync(string applicationUserId, SetProviderScheduleRequest request);

    /// <summary>Soft-matches a Practitioner (by email) to a logged-in user's schedule for the given day, for the booking form's advisory outside-hours warning. Returns null if no match/schedule exists.</summary>
    Task<ProviderScheduleDayDto?> GetByPractitionerEmailAsync(string practitionerEmail, DayOfWeek dayOfWeek);
}

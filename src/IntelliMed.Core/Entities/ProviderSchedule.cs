namespace IntelliMed.Core.Entities;

/// <summary>
/// A logged-in user's self-service weekly working hours (set from their own Profile Settings).
/// Keyed on ApplicationUserId rather than Practitioner — this codebase has no FK between
/// ApplicationUser and Practitioner, so the appointment booking form soft-matches by Email
/// instead of joining through a hard relationship here.
/// </summary>
public class ProviderSchedule
{
    public int Id { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsActive { get; set; } = true;

    public ApplicationUser? ApplicationUser { get; set; }
}

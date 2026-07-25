namespace IntelliMed.Core.Entities;

/// <summary>
/// Per-clinic mapping from an account type to the fee schedule that prices its invoice lines.
/// Legacy equivalent: AccountTypeFeeRateMappings.
/// </summary>
public class AccountTypeFeeScheduleMapping
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public AccountTypeEnum AccountType { get; set; }
    public int FeeScheduleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public FeeSchedule? FeeSchedule { get; set; }
}

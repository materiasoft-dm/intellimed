namespace IntelliMed.Core.DTOs;

public class SystemSettingsDto
{
    public int PasswordMinLength { get; set; }
    public bool PasswordRequireDigit { get; set; }
    public bool PasswordRequireLowercase { get; set; }
    public bool PasswordRequireUppercase { get; set; }
    public bool PasswordRequireNonAlphanumeric { get; set; }
    public int LockoutMaxFailedAttempts { get; set; }
    public int LockoutDurationMinutes { get; set; }
    public int SessionExpirationHours { get; set; }
    public int DefaultAppointmentSlotMinutes { get; set; }
    public bool NewUserShowCommandPalette { get; set; }
    public int BackupRetentionDays { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Same shape minus the audit field. Values are range-checked in
/// SystemSettingsRepository.UpdateAsync — a zero-length password policy or a zero-day backup
/// retention would be actively harmful, so they're rejected rather than clamped silently.</summary>
public class UpdateSystemSettingsDto
{
    public int PasswordMinLength { get; set; }
    public bool PasswordRequireDigit { get; set; }
    public bool PasswordRequireLowercase { get; set; }
    public bool PasswordRequireUppercase { get; set; }
    public bool PasswordRequireNonAlphanumeric { get; set; }
    public int LockoutMaxFailedAttempts { get; set; }
    public int LockoutDurationMinutes { get; set; }
    public int SessionExpirationHours { get; set; }
    public int DefaultAppointmentSlotMinutes { get; set; }
    public bool NewUserShowCommandPalette { get; set; }
    public int BackupRetentionDays { get; set; }
}

namespace IntelliMed.Core.Entities;

/// <summary>
/// Application-wide settings, as opposed to <see cref="ClinicSettings"/> (per-practice details,
/// billing catalogue, SMTP) which is a different thing and has its own screen at /clinic-settings.
///
/// Single row, Id = 1, seeded via HasData — same singleton shape ClinicSettings uses. Every default
/// below matches the value that was previously hardcoded, so seeding changes no behaviour.
/// </summary>
public class SystemSettings
{
    public int Id { get; set; } = 1;

    // --- Password policy -----------------------------------------------------
    // Applied to ASP.NET Identity by DatabaseIdentityOptionsConfigurator. Identity resolves its
    // options once and caches them, so changes here take effect on the next app restart — the
    // settings screen says so rather than pretending otherwise.
    public int PasswordMinLength { get; set; } = 8;
    public bool PasswordRequireDigit { get; set; } = true;
    public bool PasswordRequireLowercase { get; set; } = true;
    public bool PasswordRequireUppercase { get; set; } = true;
    public bool PasswordRequireNonAlphanumeric { get; set; } = true;

    // --- Lockout -------------------------------------------------------------
    public int LockoutMaxFailedAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;

    /// <summary>How long an issued staff JWT stays valid. Read at token-generation time, so a change
    /// applies to the next sign-in with no restart needed.</summary>
    public int SessionExpirationHours { get; set; } = 8;

    // --- New-user defaults ---------------------------------------------------
    /// <summary>Fallback when ApplicationUser.DefaultAppointmentTimeslotMinutes is null.</summary>
    public int DefaultAppointmentSlotMinutes { get; set; } = 15;
    public bool NewUserShowCommandPalette { get; set; } = true;

    /// <summary>Backups older than this are pruned by DatabaseBackupBackgroundService. Was a
    /// hardcoded 60-day constant.</summary>
    public int BackupRetentionDays { get; set; } = 60;

    public DateTime? UpdatedAt { get; set; }
}

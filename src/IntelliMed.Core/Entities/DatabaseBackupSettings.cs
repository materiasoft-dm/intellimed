namespace IntelliMed.Core.Entities;

/// <summary>Single-row settings table (Id == 1) — scheduled-backup interval configuration, mirrors ClinicSettings.</summary>
public class DatabaseBackupSettings
{
    public int Id { get; set; }
    public int IntervalValue { get; set; } = 1;
    public BackupIntervalUnit IntervalUnit { get; set; } = BackupIntervalUnit.Days;

    /// <summary>When the last scheduled (not manual) backup ran — drives when the next one is due.</summary>
    public DateTime? LastRunAt { get; set; }
}

public enum BackupIntervalUnit
{
    Minutes,
    Hours,
    Days,
    Weeks,
    Months
}

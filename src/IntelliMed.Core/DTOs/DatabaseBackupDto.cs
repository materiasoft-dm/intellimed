using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public enum DatabaseBackupTrigger
{
    Manual,
    Scheduled
}

public class DatabaseBackupDto
{
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DatabaseBackupTrigger Trigger { get; set; }
    public string TriggerName => Trigger.ToString();
    public DateTime CreatedAt { get; set; }
}

public class DatabaseBackupSettingsDto
{
    public int IntervalValue { get; set; }
    public BackupIntervalUnit IntervalUnit { get; set; }
    public string IntervalUnitName => IntervalUnit.ToString();
    public DateTime? LastRunAt { get; set; }
}

public class UpdateDatabaseBackupSettingsDto
{
    public int IntervalValue { get; set; }
    public BackupIntervalUnit IntervalUnit { get; set; }
}

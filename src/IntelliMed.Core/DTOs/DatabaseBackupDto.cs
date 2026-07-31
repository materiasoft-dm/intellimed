using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class DatabaseBackupDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DatabaseBackupTrigger Trigger { get; set; }
    public string TriggerName => Trigger.ToString();
    public DateTime CreatedAt { get; set; }
}

/// <summary>Internal-only — passed between DatabaseBackupService and IDatabaseBackupRepository, never bound from an HTTP request body.</summary>
public class CreateDatabaseBackupDto
{
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DatabaseBackupTrigger Trigger { get; set; }
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

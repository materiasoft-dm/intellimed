namespace IntelliMed.Core.Entities;

public class DatabaseBackup
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DatabaseBackupTrigger Trigger { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum DatabaseBackupTrigger
{
    Manual,
    Scheduled
}

using System.Data;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Performs an online SQLite backup via SqliteConnection.BackupDatabase — this wraps SQLite's own
/// backup API and is safe to run against the live database with concurrent writers, unlike a raw
/// file copy which could capture a half-written page. Backups are written to a "backups" folder
/// alongside the live .db file (same directory SqliteConnection.DataSource resolves to), so they
/// share whatever persistence/volume the live database itself has.
///
/// The filesystem is the source of truth — there is no DatabaseBackups table. An earlier version
/// tracked one DB row per backup file; two backups landing in the same second could then produce two
/// rows sharing one filename, silently orphaning one row's metadata when the file got overwritten.
/// Everything reported here (FileSizeBytes, CreatedAt) comes straight from FileInfo; the one detail a
/// bare file doesn't carry — which trigger created it — is encoded into the filename instead.
/// </summary>
public class DatabaseBackupService : IDatabaseBackupService
{
    private const string FilePrefix = "intellimed-backup-";

    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(AppDbContext context, ILogger<DatabaseBackupService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<IEnumerable<DatabaseBackupDto>> GetAllAsync()
    {
        var backupsDir = GetBackupsDirectory();
        if (!Directory.Exists(backupsDir))
            return Task.FromResult(Enumerable.Empty<DatabaseBackupDto>());

        var backups = Directory.GetFiles(backupsDir, $"{FilePrefix}*.db")
            .Select(path => ToDto(new FileInfo(path)))
            .OrderByDescending(b => b.CreatedAt)
            .ToList();

        return Task.FromResult<IEnumerable<DatabaseBackupDto>>(backups);
    }

    public async Task<DatabaseBackupDto> PerformBackupAsync(DatabaseBackupTrigger trigger)
    {
        var backupsDir = GetBackupsDirectory();
        Directory.CreateDirectory(backupsDir);

        var triggerToken = trigger == DatabaseBackupTrigger.Manual ? "manual" : "scheduled";
        // Millisecond precision so two backups triggered in quick succession — e.g. a scheduled run
        // landing right as someone clicks "Backup Now" — never collide on filename.
        var fileName = $"{FilePrefix}{triggerToken}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.db";
        var filePath = Path.Combine(backupsDir, fileName);

        var sourceConnection = (SqliteConnection)_context.Database.GetDbConnection();
        var wasClosed = sourceConnection.State != ConnectionState.Open;
        if (wasClosed) await sourceConnection.OpenAsync();

        try
        {
            // Pooling=False: Microsoft.Data.Sqlite pools connections by connection string, so a
            // pooled connection's native file handle stays open after Dispose() — which then makes a
            // later File.ReadAllBytesAsync/File.Delete fail with a sharing violation on Windows,
            // since the OS still sees the backup file as in use. This connection is only ever opened
            // once, so pooling buys nothing and only costs us a held handle.
            using var destinationConnection = new SqliteConnection($"Data Source={filePath};Pooling=False");
            destinationConnection.Open();
            sourceConnection.BackupDatabase(destinationConnection);
        }
        finally
        {
            if (wasClosed) await sourceConnection.CloseAsync();
        }

        var info = new FileInfo(filePath);
        _logger.LogInformation("Database backup created: {FileName} ({SizeBytes} bytes, {Trigger})", fileName, info.Length, trigger);

        return ToDto(info);
    }

    public async Task<(byte[] Content, string FileName)?> GetBackupFileAsync(string fileName)
    {
        var filePath = ResolveSafePath(fileName);
        if (filePath == null || !File.Exists(filePath)) return null;

        var content = await File.ReadAllBytesAsync(filePath);
        return (content, fileName);
    }

    public Task DeleteAsync(string fileName)
    {
        var filePath = ResolveSafePath(fileName);
        if (filePath != null && File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Database backup deleted: {FileName}", fileName);
        }
        return Task.CompletedTask;
    }

    public async Task<int> DeleteExpiredAsync(DateTime olderThan)
    {
        var expired = (await GetAllAsync()).Where(b => b.CreatedAt < olderThan).ToList();
        foreach (var backup in expired)
        {
            await DeleteAsync(backup.FileName);
        }
        return expired.Count;
    }

    private static DatabaseBackupDto ToDto(FileInfo info) => new()
    {
        FileName = info.Name,
        FileSizeBytes = info.Length,
        Trigger = ParseTrigger(info.Name),
        CreatedAt = info.CreationTimeUtc
    };

    // Filenames from before this trigger-in-filename scheme (no "-manual-"/"-scheduled-" token) default
    // to Scheduled — a one-time, cosmetic-only label guess for pre-existing files; their size/date/
    // existence are unaffected.
    private static DatabaseBackupTrigger ParseTrigger(string fileName) =>
        fileName.Contains("-manual-", StringComparison.OrdinalIgnoreCase)
            ? DatabaseBackupTrigger.Manual
            : DatabaseBackupTrigger.Scheduled;

    /// <summary>
    /// fileName arrives from an HTTP route (download/delete), so it's attacker-controllable — strips
    /// any directory component and rejects anything that doesn't look like a real backup file, so it
    /// can't be used for path traversal. Returns null (treated as not-found) rather than throwing.
    /// </summary>
    private string? ResolveSafePath(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        if (safeName != fileName
            || !safeName.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase)
            || !safeName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.Combine(GetBackupsDirectory(), safeName);
    }

    private string GetBackupsDirectory()
    {
        var connection = (SqliteConnection)_context.Database.GetDbConnection();
        var dbPath = Path.GetFullPath(connection.DataSource);
        var dbDirectory = Path.GetDirectoryName(dbPath)!;
        return Path.Combine(dbDirectory, "backups");
    }
}

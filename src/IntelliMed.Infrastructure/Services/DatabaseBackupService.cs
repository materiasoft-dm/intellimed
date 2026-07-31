using System.Data;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
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
/// </summary>
public class DatabaseBackupService : IDatabaseBackupService
{
    private readonly AppDbContext _context;
    private readonly IDatabaseBackupRepository _repository;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(AppDbContext context, IDatabaseBackupRepository repository, ILogger<DatabaseBackupService> logger)
    {
        _context = context;
        _repository = repository;
        _logger = logger;
    }

    public async Task<DatabaseBackupDto> PerformBackupAsync(DatabaseBackupTrigger trigger)
    {
        var backupsDir = GetBackupsDirectory();
        Directory.CreateDirectory(backupsDir);

        var fileName = $"intellimed-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db";
        var filePath = Path.Combine(backupsDir, fileName);

        var sourceConnection = (SqliteConnection)_context.Database.GetDbConnection();
        var wasClosed = sourceConnection.State != ConnectionState.Open;
        if (wasClosed) await sourceConnection.OpenAsync();

        try
        {
            // Pooling=False: Microsoft.Data.Sqlite pools connections by connection string, so a
            // pooled connection's native file handle stays open after Dispose() — which then makes
            // GetBackupFileAsync's File.ReadAllBytesAsync fail with a sharing violation on Windows,
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

        var fileSizeBytes = new FileInfo(filePath).Length;
        var id = await _repository.CreateAsync(new CreateDatabaseBackupDto
        {
            FileName = fileName,
            FileSizeBytes = fileSizeBytes,
            Trigger = trigger
        });

        _logger.LogInformation("Database backup created: {FileName} ({SizeBytes} bytes, {Trigger})", fileName, fileSizeBytes, trigger);

        return (await _repository.GetByIdAsync(id))!;
    }

    public async Task<(byte[] Content, string FileName)?> GetBackupFileAsync(int id)
    {
        var backup = await _repository.GetByIdAsync(id);
        if (backup == null) return null;

        var filePath = Path.Combine(GetBackupsDirectory(), backup.FileName);
        if (!File.Exists(filePath)) return null;

        var content = await File.ReadAllBytesAsync(filePath);
        return (content, backup.FileName);
    }

    private string GetBackupsDirectory()
    {
        var connection = (SqliteConnection)_context.Database.GetDbConnection();
        var dbPath = Path.GetFullPath(connection.DataSource);
        var dbDirectory = Path.GetDirectoryName(dbPath)!;
        return Path.Combine(dbDirectory, "backups");
    }
}

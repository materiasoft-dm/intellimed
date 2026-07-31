using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntelliMed.Tests.Services;

/// <summary>
/// Unlike the rest of the suite (EF InMemory provider), this exercises a real file-backed SQLite
/// database — DatabaseBackupService's whole job is SqliteConnection.BackupDatabase, which the
/// InMemory provider doesn't support (GetDbConnection() throws against it), so a real provider is
/// required to test the actual backup mechanics rather than just mocking around them.
/// </summary>
public class DatabaseBackupServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly AppDbContext _context;
    private readonly DatabaseBackupRepository _repository;
    private readonly DatabaseBackupService _service;

    public DatabaseBackupServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "intellimed-backup-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "test.db");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new DatabaseBackupRepository(_context);
        _service = new DatabaseBackupService(_context, _repository, NullLogger<DatabaseBackupService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task PerformBackupAsync_CreatesReadableSqliteFileAndRecord()
    {
        var result = await _service.PerformBackupAsync(DatabaseBackupTrigger.Manual);

        result.FileSizeBytes.Should().BeGreaterThan(0);
        result.Trigger.Should().Be(DatabaseBackupTrigger.Manual);

        var backupFilePath = Path.Combine(_tempDir, "backups", result.FileName);
        File.Exists(backupFilePath).Should().BeTrue();

        // The backup file must itself be a valid, independently-openable SQLite database
        // containing the same schema as the source (spot-check one known table).
        await using var verifyConnection = new SqliteConnection($"Data Source={backupFilePath}");
        await verifyConnection.OpenAsync();
        await using var command = verifyConnection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='DatabaseBackups'";
        var tableName = await command.ExecuteScalarAsync();
        tableName.Should().Be("DatabaseBackups");
    }

    [Fact]
    public async Task GetBackupFileAsync_ReturnsMatchingBytesForKnownBackup()
    {
        var created = await _service.PerformBackupAsync(DatabaseBackupTrigger.Scheduled);

        var file = await _service.GetBackupFileAsync(created.Id);

        file.Should().NotBeNull();
        file!.Value.FileName.Should().Be(created.FileName);
        file.Value.Content.Length.Should().Be((int)created.FileSizeBytes);
    }

    [Fact]
    public async Task GetBackupFileAsync_UnknownId_ReturnsNull()
    {
        var file = await _service.GetBackupFileAsync(999);

        file.Should().BeNull();
    }
}

using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Infrastructure.Data;
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
/// required to test the actual backup mechanics rather than just mocking around them. The service is
/// filesystem-as-source-of-truth (no DatabaseBackups table), so these assert against real files in a
/// throwaway temp directory rather than a repository.
/// </summary>
public class DatabaseBackupServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly AppDbContext _context;
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

        _service = new DatabaseBackupService(_context, NullLogger<DatabaseBackupService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task PerformBackupAsync_CreatesReadableSqliteFileEncodingTriggerInName()
    {
        var result = await _service.PerformBackupAsync(DatabaseBackupTrigger.Manual);

        result.FileSizeBytes.Should().BeGreaterThan(0);
        result.Trigger.Should().Be(DatabaseBackupTrigger.Manual);
        result.FileName.Should().Contain("-manual-");

        var backupFilePath = Path.Combine(_tempDir, "backups", result.FileName);
        File.Exists(backupFilePath).Should().BeTrue();

        // The backup file must itself be a valid, independently-openable SQLite database
        // containing the same schema as the source (spot-check one known table).
        await using var verifyConnection = new SqliteConnection($"Data Source={backupFilePath}");
        await verifyConnection.OpenAsync();
        await using var command = verifyConnection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='DatabaseBackupSettings'";
        var tableName = await command.ExecuteScalarAsync();
        tableName.Should().Be("DatabaseBackupSettings");
    }

    [Fact]
    public async Task PerformBackupAsync_TwoBackupsInQuickSuccession_GetDistinctFileNames()
    {
        var first = await _service.PerformBackupAsync(DatabaseBackupTrigger.Scheduled);
        var second = await _service.PerformBackupAsync(DatabaseBackupTrigger.Manual);

        first.FileName.Should().NotBe(second.FileName);
        File.Exists(Path.Combine(_tempDir, "backups", first.FileName)).Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, "backups", second.FileName)).Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsNewestFirst()
    {
        var older = await _service.PerformBackupAsync(DatabaseBackupTrigger.Scheduled);
        var newer = await _service.PerformBackupAsync(DatabaseBackupTrigger.Manual);

        var result = (await _service.GetAllAsync()).ToList();

        result.Should().HaveCount(2);
        result[0].FileName.Should().Be(newer.FileName);
        result[1].FileName.Should().Be(older.FileName);
    }

    [Fact]
    public async Task GetAllAsync_NoBackupsDirectoryYet_ReturnsEmpty()
    {
        var result = await _service.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBackupFileAsync_ReturnsMatchingBytesForKnownBackup()
    {
        var created = await _service.PerformBackupAsync(DatabaseBackupTrigger.Scheduled);

        var file = await _service.GetBackupFileAsync(created.FileName);

        file.Should().NotBeNull();
        file!.Value.FileName.Should().Be(created.FileName);
        file.Value.Content.Length.Should().Be((int)created.FileSizeBytes);
    }

    [Fact]
    public async Task GetBackupFileAsync_UnknownFileName_ReturnsNull()
    {
        var file = await _service.GetBackupFileAsync("intellimed-backup-manual-99990101-000000000.db");

        file.Should().BeNull();
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\config")]
    [InlineData("not-a-backup-file.txt")]
    [InlineData("intellimed-backup-manual-20260101-000000000.db/../../secrets.db")]
    public async Task GetBackupFileAsync_PathTraversalOrWrongPrefix_ReturnsNull(string maliciousName)
    {
        var file = await _service.GetBackupFileAsync(maliciousName);

        file.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheFile()
    {
        var created = await _service.PerformBackupAsync(DatabaseBackupTrigger.Manual);
        var backupFilePath = Path.Combine(_tempDir, "backups", created.FileName);
        File.Exists(backupFilePath).Should().BeTrue();

        await _service.DeleteAsync(created.FileName);

        File.Exists(backupFilePath).Should().BeFalse();
        (await _service.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_UnknownFileName_DoesNotThrow()
    {
        var act = async () => await _service.DeleteAsync("intellimed-backup-manual-99990101-000000000.db");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_PathTraversalAttempt_DoesNotThrowAndLeavesOtherFilesAlone()
    {
        var created = await _service.PerformBackupAsync(DatabaseBackupTrigger.Manual);

        await _service.DeleteAsync("../../../" + created.FileName);

        File.Exists(Path.Combine(_tempDir, "backups", created.FileName)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteExpiredAsync_DeletesOnlyBackupsOlderThanCutoff()
    {
        var oldBackup = await _service.PerformBackupAsync(DatabaseBackupTrigger.Scheduled);
        var recentBackup = await _service.PerformBackupAsync(DatabaseBackupTrigger.Manual);

        // CreatedAt comes from the file's own CreationTimeUtc — backdate the first file directly to
        // simulate a backup that's actually aged past the retention window.
        var oldFilePath = Path.Combine(_tempDir, "backups", oldBackup.FileName);
        File.SetCreationTimeUtc(oldFilePath, DateTime.UtcNow.AddDays(-61));

        var deletedCount = await _service.DeleteExpiredAsync(DateTime.UtcNow.AddDays(-60));

        deletedCount.Should().Be(1);
        var remaining = (await _service.GetAllAsync()).ToList();
        remaining.Should().ContainSingle(b => b.FileName == recentBackup.FileName);
        File.Exists(oldFilePath).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, "backups", recentBackup.FileName)).Should().BeTrue();
    }
}

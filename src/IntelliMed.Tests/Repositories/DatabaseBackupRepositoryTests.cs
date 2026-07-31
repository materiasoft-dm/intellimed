using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class DatabaseBackupRepositoryTests : IDisposable
{
    private readonly DatabaseBackupRepository _repository;
    private readonly AppDbContext _context;

    public DatabaseBackupRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new DatabaseBackupRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewBackupId()
    {
        var dto = new CreateDatabaseBackupDto { FileName = "backup-1.db", FileSizeBytes = 1024, Trigger = DatabaseBackupTrigger.Manual };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var backup = await _context.DatabaseBackups.FindAsync(result);
        backup!.FileName.Should().Be("backup-1.db");
        backup.Trigger.Should().Be(DatabaseBackupTrigger.Manual);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsNewestFirst()
    {
        _context.DatabaseBackups.Add(new DatabaseBackup { FileName = "old.db", CreatedAt = DateTime.UtcNow.AddHours(-1) });
        _context.DatabaseBackups.Add(new DatabaseBackup { FileName = "new.db", CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetAllAsync()).ToList();

        result.Should().HaveCount(2);
        result[0].FileName.Should().Be("new.db");
        result[1].FileName.Should().Be("old.db");
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(999);

        result.Should().BeNull();
    }
}

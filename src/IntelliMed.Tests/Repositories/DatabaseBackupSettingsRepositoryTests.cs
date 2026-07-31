using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class DatabaseBackupSettingsRepositoryTests : IDisposable
{
    private readonly DatabaseBackupSettingsRepository _repository;
    private readonly AppDbContext _context;

    public DatabaseBackupSettingsRepositoryTests()
    {
        // The Id=1 row is seeded via AppDbContext's HasData, applied by EnsureCreated() inside
        // TestDbContextFactory — no manual seeding needed here.
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new DatabaseBackupSettingsRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetSingletonAsync_ReturnsSeededDefault()
    {
        var result = await _repository.GetSingletonAsync();

        result.IntervalValue.Should().Be(1);
        result.IntervalUnit.Should().Be(BackupIntervalUnit.Days);
        result.LastRunAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSingletonAsync_ChangesIntervalAndUnit()
    {
        await _repository.UpdateSingletonAsync(new UpdateDatabaseBackupSettingsDto { IntervalValue = 30, IntervalUnit = BackupIntervalUnit.Minutes });

        var result = await _repository.GetSingletonAsync();
        result.IntervalValue.Should().Be(30);
        result.IntervalUnit.Should().Be(BackupIntervalUnit.Minutes);
    }

    [Fact]
    public async Task MarkRanAsync_SetsLastRunAt()
    {
        var ranAt = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

        await _repository.MarkRanAsync(ranAt);

        var result = await _repository.GetSingletonAsync();
        result.LastRunAt.Should().Be(ranAt);
    }
}

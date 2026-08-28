using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class HealthFundRepositoryTests : IDisposable
{
    private readonly HealthFundRepository _repository;
    private readonly AppDbContext _context;

    public HealthFundRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new HealthFundRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetAllActiveAsync_ReturnsTheSeededFunds()
    {
        // HealthFunds are seeded via HasData in AppDbContext (TestDbContextFactory calls
        // EnsureCreated so the InMemory provider actually applies them).
        var results = await _repository.GetAllActiveAsync();

        results.Should().NotBeEmpty();
        results.Select(f => f.Code).Should().Contain("BUP");
    }

    [Fact]
    public async Task GetAllActiveAsync_IsOrderedByName()
    {
        var results = (await _repository.GetAllActiveAsync()).ToList();

        // Case-insensitive on purpose: the provider's collation decides where a lowercase name like
        // "nib" lands relative to "Teachers Health", and that differs between the InMemory provider
        // used here and real SQLite. The behaviour worth pinning is "alphabetical to a human", not
        // whichever ordinal collation happens to be in play.
        results.Select(f => f.Name)
            .Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAllActiveAsync_ExcludesInactiveFunds()
    {
        var fund = await _context.HealthFunds.FirstAsync();
        fund.IsActive = false;
        await _context.SaveChangesAsync();

        var results = await _repository.GetAllActiveAsync();

        results.Should().NotContain(f => f.Id == fund.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTheMappedFund()
    {
        var seeded = await _context.HealthFunds.FirstAsync();

        var result = await _repository.GetByIdAsync(seeded.Id);

        result.Should().NotBeNull();
        result!.Code.Should().Be(seeded.Code);
        result.Name.Should().Be(seeded.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForUnknownId()
    {
        var result = await _repository.GetByIdAsync(9999);

        result.Should().BeNull();
    }
}

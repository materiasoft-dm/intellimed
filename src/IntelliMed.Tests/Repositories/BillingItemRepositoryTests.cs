using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class BillingItemRepositoryTests : IDisposable
{
    private readonly BillingItemRepository _repository;
    private readonly AppDbContext _context;

    public BillingItemRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new BillingItemRepository(_context);

        _context.BillingItems.AddRange(
            new BillingItem { ItemNumber = "23", Description = "Standard GP consultation", ScheduleFee = 41.40m, IsActive = true },
            new BillingItem { ItemNumber = "36", Description = "Long GP consultation", ScheduleFee = 79.70m, IsActive = true },
            new BillingItem { ItemNumber = "104", Description = "Specialist referred consultation", ScheduleFee = 85.60m, IsActive = false });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task SearchAsync_MatchingItemNumberPrefix_ReturnsItem()
    {
        var result = (await _repository.SearchAsync("23")).ToList();

        result.Should().ContainSingle();
        result[0].ItemNumber.Should().Be("23");
    }

    [Fact]
    public async Task SearchAsync_MatchingDescription_ReturnsItem()
    {
        var result = (await _repository.SearchAsync("long")).ToList();

        result.Should().ContainSingle();
        result[0].ItemNumber.Should().Be("36");
    }

    [Fact]
    public async Task SearchAsync_ExcludesInactiveItems()
    {
        var result = (await _repository.SearchAsync("104")).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsActiveItemsOrderedByItemNumber()
    {
        var result = (await _repository.SearchAsync(string.Empty)).ToList();

        result.Should().HaveCount(2);
        result.Select(r => r.ItemNumber).Should().ContainInOrder("23", "36");
    }
}

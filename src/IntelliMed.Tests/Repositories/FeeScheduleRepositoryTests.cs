using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class FeeScheduleRepositoryTests : IDisposable
{
    private readonly FeeScheduleRepository _repository;
    private readonly AppDbContext _context;

    public FeeScheduleRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new FeeScheduleRepository(_context);

        _context.BillingItems.AddRange(
            new BillingItem { ItemNumber = "23", Description = "Standard GP consultation", ScheduleFee = 41.40m, Benefit100 = 41.40m, IsActive = true },
            new BillingItem { ItemNumber = "36", Description = "Long GP consultation", ScheduleFee = 79.70m, Benefit100 = 79.70m, IsActive = true });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private async Task<int> CreateScheduleAsync(string code = "TEST") =>
        await _repository.CreateAsync(new CreateFeeScheduleDto { Code = code, Description = "Test schedule" });

    [Fact]
    public async Task ImportItemsAsync_CalledTwiceWithSameRequest_IsIdempotent()
    {
        var scheduleId = await CreateScheduleAsync();
        var request = new ImportFeeScheduleItemsRequest { SourceFeeScheduleId = null, Percent = 0, FlatAmount = 0 };

        var firstRun = await _repository.ImportItemsAsync(scheduleId, request);
        var secondRun = await _repository.ImportItemsAsync(scheduleId, request);

        firstRun.Should().Be(2);
        secondRun.Should().Be(0);
        _context.FeeScheduleItems.Count(i => i.FeeScheduleId == scheduleId).Should().Be(2);
    }

    [Fact]
    public async Task AddItemAsync_CalledTwiceForSameBillingItem_UpsertsInsteadOfThrowing()
    {
        var scheduleId = await CreateScheduleAsync();
        var billingItemId = _context.BillingItems.Single(b => b.ItemNumber == "23").Id;

        var firstId = await _repository.AddItemAsync(scheduleId, new CreateFeeScheduleItemDto { BillingItemId = billingItemId, Fee = 40m });
        var secondId = await _repository.AddItemAsync(scheduleId, new CreateFeeScheduleItemDto { BillingItemId = billingItemId, Fee = 45m });

        secondId.Should().Be(firstId);
        _context.FeeScheduleItems.Count(i => i.FeeScheduleId == scheduleId).Should().Be(1);
        _context.FeeScheduleItems.Single(i => i.Id == firstId).Fee.Should().Be(45m);
    }

    [Fact]
    public async Task SeedBulkBillSchedulesAsync_CalledTwice_DoesNotDuplicateSchedulesOrItems()
    {
        var firstRun = await _repository.SeedBulkBillSchedulesAsync();
        var secondRun = await _repository.SeedBulkBillSchedulesAsync();

        firstRun.SchedulesCreated.Should().Be(3);
        firstRun.ItemsAffected.Should().Be(6); // 3 schedules x 2 billing items
        secondRun.SchedulesCreated.Should().Be(0);
        secondRun.ItemsAffected.Should().Be(0);
        _context.FeeSchedules.Count(f => f.Code == "BBGP").Should().Be(1);
    }

    [Fact]
    public async Task SeedDvaScheduleShellsAsync_CalledTwice_DoesNotDuplicateSchedules()
    {
        var firstRun = await _repository.SeedDvaScheduleShellsAsync();
        var secondRun = await _repository.SeedDvaScheduleShellsAsync();

        firstRun.SchedulesCreated.Should().Be(3);
        firstRun.ItemsAffected.Should().Be(0); // shells only — no authoritative DVA fee source to auto-populate from
        secondRun.SchedulesCreated.Should().Be(0);
        secondRun.ItemsAffected.Should().Be(0);
        _context.FeeSchedules.Count(f => f.Code == "VAGP").Should().Be(1);
    }

    [Fact]
    public async Task UpdateItemFeeAsync_WithUnchangedFee_DoesNotRecordHistory()
    {
        var scheduleId = await CreateScheduleAsync();
        var billingItemId = _context.BillingItems.Single(b => b.ItemNumber == "23").Id;
        var itemId = await _repository.AddItemAsync(scheduleId, new CreateFeeScheduleItemDto { BillingItemId = billingItemId, Fee = 40m });

        await _repository.UpdateItemFeeAsync(itemId, 40m);

        (await _repository.GetItemHistoryAsync(itemId)).Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateItemFeeAsync_WithChangedFee_AppendsPriceHistoryMostRecentFirst()
    {
        var scheduleId = await CreateScheduleAsync();
        var billingItemId = _context.BillingItems.Single(b => b.ItemNumber == "23").Id;
        var itemId = await _repository.AddItemAsync(scheduleId, new CreateFeeScheduleItemDto { BillingItemId = billingItemId, Fee = 40m });

        await _repository.UpdateItemFeeAsync(itemId, 45m);
        await _repository.UpdateItemFeeAsync(itemId, 50m);

        var history = (await _repository.GetItemHistoryAsync(itemId)).ToList();

        history.Should().HaveCount(3);
        history.Select(h => h.Fee).Should().ContainInOrder(50m, 45m, 40m);
    }

    [Fact]
    public async Task ImportItemsAsync_ReimportedAfterCatalogFeeChange_RecordsOnlyOneNewHistoryEntry()
    {
        var scheduleId = await CreateScheduleAsync();
        var request = new ImportFeeScheduleItemsRequest { SourceFeeScheduleId = null, Percent = 0, FlatAmount = 0 };
        await _repository.ImportItemsAsync(scheduleId, request);

        var billingItem = _context.BillingItems.Single(b => b.ItemNumber == "23");
        billingItem.ScheduleFee = 50.00m;
        await _context.SaveChangesAsync();

        await _repository.ImportItemsAsync(scheduleId, request);

        var itemId = _context.FeeScheduleItems.Single(i => i.FeeScheduleId == scheduleId && i.BillingItemId == billingItem.Id).Id;
        var history = (await _repository.GetItemHistoryAsync(itemId)).ToList();

        history.Should().HaveCount(2);
        history[0].Fee.Should().Be(50.00m);
        history[1].Fee.Should().Be(41.40m);
    }
}

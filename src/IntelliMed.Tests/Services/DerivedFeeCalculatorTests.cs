using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Services;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Services;

public class DerivedFeeCalculatorTests : IDisposable
{
    private readonly DerivedFeeCalculator _calculator;
    private readonly AppDbContext _context;
    private readonly BillingItem _primaryItem;
    private readonly BillingItem _derivedItem;

    public DerivedFeeCalculatorTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _calculator = new DerivedFeeCalculator(_context);

        _primaryItem = new BillingItem { ItemNumber = "30571", Description = "Primary surgical procedure", ScheduleFee = 500m, IsActive = true };
        _derivedItem = new BillingItem { ItemNumber = "51300", Description = "Assistant at surgery", ScheduleFee = 0m, IsActive = true };
        _context.BillingItems.AddRange(_primaryItem, _derivedItem);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private InvoiceItem MakeItem(BillingItem billingItem, decimal unitPrice = 0, decimal rebatePerUnit = 0, decimal? derivedQuantity = null) => new()
    {
        BillingItemId = billingItem.Id,
        Description = billingItem.Description,
        Quantity = 1,
        UnitPrice = unitPrice,
        RebatePerUnit = rebatePerUnit,
        DerivedQuantity = derivedQuantity
    };

    [Fact]
    public async Task ApplyDerivedFeesAsync_PercentageOfAssociatedItem_ComputesCorrectFee()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.PercentageOfAssociatedItem,
            AssociatedBillingItemId = _primaryItem.Id,
            Percentage = 20m
        });
        await _context.SaveChangesAsync();

        var primaryLine = MakeItem(_primaryItem, unitPrice: 500m, rebatePerUnit: 400m);
        var assistantLine = MakeItem(_derivedItem);
        var items = new List<InvoiceItem> { primaryLine, assistantLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        assistantLine.UnitPrice.Should().Be(100m);
        assistantLine.RebatePerUnit.Should().Be(80m);
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_AssistanceAnaesthesia_ComputesPlainPercentage_NoTimeLoading()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.AssistanceAnaesthesia,
            AssociatedBillingItemId = _primaryItem.Id,
            Percentage = 50m,
            UnitValue = 999m // must be ignored — AssistanceAnaesthesia no longer adds a time-loading term
        });
        await _context.SaveChangesAsync();

        var primaryLine = MakeItem(_primaryItem, unitPrice: 200m, rebatePerUnit: 160m);
        var assistantLine = MakeItem(_derivedItem, derivedQuantity: 30m); // must also be ignored
        var items = new List<InvoiceItem> { primaryLine, assistantLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        assistantLine.UnitPrice.Should().Be(100m); // 50% of 200, not 100 + (999*30)
        assistantLine.RebatePerUnit.Should().Be(80m);
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_NumberOfPatientsSeen_MultipliesUnitValueByDerivedQuantity()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.NumberOfPatientsSeen,
            UnitValue = 15m
        });
        await _context.SaveChangesAsync();

        var line = MakeItem(_derivedItem, derivedQuantity: 4m);
        var items = new List<InvoiceItem> { line };

        await _calculator.ApplyDerivedFeesAsync(items);

        line.UnitPrice.Should().Be(60m);
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_TimeDuration_OverLimit_UsesOverLimitRate()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.TimeDuration,
            UnitValue = 3m,
            LimitQuantity = 20m,
            OverLimitUnitValue = 1.5m
        });
        await _context.SaveChangesAsync();

        var line = MakeItem(_derivedItem, derivedQuantity: 30m);
        var items = new List<InvoiceItem> { line };

        await _calculator.ApplyDerivedFeesAsync(items);

        // 20 minutes at $3 + 10 minutes over the limit at $1.50 = 60 + 15 = 75
        line.UnitPrice.Should().Be(75m);
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_NoConfigForItem_LeavesNormallyResolvedFeeUnchanged()
    {
        var line = MakeItem(_primaryItem, unitPrice: 500m, rebatePerUnit: 400m);
        var items = new List<InvoiceItem> { line };

        await _calculator.ApplyDerivedFeesAsync(items);

        line.UnitPrice.Should().Be(500m);
        line.RebatePerUnit.Should().Be(400m);
    }
}

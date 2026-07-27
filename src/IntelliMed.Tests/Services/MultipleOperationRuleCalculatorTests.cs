using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Services;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Services;

public class MultipleOperationRuleCalculatorTests : IDisposable
{
    private readonly MultipleOperationRuleCalculator _calculator;
    private readonly AppDbContext _context;

    public MultipleOperationRuleCalculatorTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _calculator = new MultipleOperationRuleCalculator(_context);
    }

    public void Dispose() => _context.Dispose();

    private BillingItem MakeBillingItem(string itemNumber, string? group)
    {
        var item = new BillingItem { ItemNumber = itemNumber, Description = itemNumber, Group = group, ScheduleFee = 0m, IsActive = true };
        _context.BillingItems.Add(item);
        _context.SaveChanges();
        return item;
    }

    private static InvoiceItem MakeLine(BillingItem billingItem, decimal unitPrice, decimal rebatePerUnit) => new()
    {
        BillingItemId = billingItem.Id,
        Description = billingItem.Description,
        Quantity = 1,
        UnitPrice = unitPrice,
        RebatePerUnit = rebatePerUnit
    };

    [Fact]
    public async Task ApplyMultipleOperationRuleAsync_TwoOperationsItems_AbatesSecondTo50Percent()
    {
        var highest = MakeBillingItem("30001", "T8");
        var second = MakeBillingItem("30002", "T8");
        var lines = new List<InvoiceItem>
        {
            MakeLine(second, 300m, 250m),
            MakeLine(highest, 500m, 400m)
        };

        await _calculator.ApplyMultipleOperationRuleAsync(lines);

        lines.Single(l => l.BillingItemId == highest.Id).UnitPrice.Should().Be(500m);
        lines.Single(l => l.BillingItemId == highest.Id).RebatePerUnit.Should().Be(400m);
        lines.Single(l => l.BillingItemId == second.Id).UnitPrice.Should().Be(150m);
        lines.Single(l => l.BillingItemId == second.Id).RebatePerUnit.Should().Be(125m);
    }

    [Fact]
    public async Task ApplyMultipleOperationRuleAsync_ThreeOperationsItems_Applies100_50_25Percent()
    {
        var first = MakeBillingItem("30001", "T8");
        var secondItem = MakeBillingItem("30002", "T8");
        var third = MakeBillingItem("30003", "T8");
        var lines = new List<InvoiceItem>
        {
            MakeLine(first, 500m, 400m),
            MakeLine(secondItem, 300m, 250m),
            MakeLine(third, 200m, 150m)
        };

        await _calculator.ApplyMultipleOperationRuleAsync(lines);

        lines.Single(l => l.BillingItemId == first.Id).UnitPrice.Should().Be(500m);
        lines.Single(l => l.BillingItemId == secondItem.Id).UnitPrice.Should().Be(150m);
        lines.Single(l => l.BillingItemId == third.Id).UnitPrice.Should().Be(50m);
    }

    [Fact]
    public async Task ApplyMultipleOperationRuleAsync_SingleOperationsItem_LeavesFeeUnchanged()
    {
        var only = MakeBillingItem("30001", "T8");
        var lines = new List<InvoiceItem> { MakeLine(only, 500m, 400m) };

        await _calculator.ApplyMultipleOperationRuleAsync(lines);

        lines[0].UnitPrice.Should().Be(500m);
        lines[0].RebatePerUnit.Should().Be(400m);
    }

    [Fact]
    public async Task ApplyMultipleOperationRuleAsync_NonOperationsItemsMixedIn_LeavesThemUntouched()
    {
        var op1 = MakeBillingItem("30001", "T8");
        var op2 = MakeBillingItem("30002", "T8");
        var consult = MakeBillingItem("23", "A1");
        var lines = new List<InvoiceItem>
        {
            MakeLine(op1, 500m, 400m),
            MakeLine(op2, 300m, 250m),
            MakeLine(consult, 80m, 80m)
        };

        await _calculator.ApplyMultipleOperationRuleAsync(lines);

        lines.Single(l => l.BillingItemId == consult.Id).UnitPrice.Should().Be(80m);
        lines.Single(l => l.BillingItemId == op2.Id).UnitPrice.Should().Be(150m);
    }

    [Fact]
    public async Task ApplyMultipleOperationRuleAsync_LineWithNoBillingItemId_IsSkippedWithoutThrowing()
    {
        var lines = new List<InvoiceItem>
        {
            new() { BillingItemId = null, Description = "Custom line", Quantity = 1, UnitPrice = 100m, RebatePerUnit = 0m }
        };

        var act = async () => await _calculator.ApplyMultipleOperationRuleAsync(lines);

        await act.Should().NotThrowAsync();
        lines[0].UnitPrice.Should().Be(100m);
    }
}

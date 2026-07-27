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

    private BillingItem MakeBillingItem(string itemNumber, string? group, string? subGroup = null)
    {
        var item = new BillingItem { ItemNumber = itemNumber, Description = itemNumber, Group = group, SubGroup = subGroup, ScheduleFee = 0m, IsActive = true };
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

        await _calculator.ApplyMultipleOperationRuleAsync(lines, AccountTypeEnum.PrivatePatient);

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

        await _calculator.ApplyMultipleOperationRuleAsync(lines, AccountTypeEnum.PrivatePatient);

        lines.Single(l => l.BillingItemId == first.Id).UnitPrice.Should().Be(500m);
        lines.Single(l => l.BillingItemId == secondItem.Id).UnitPrice.Should().Be(150m);
        lines.Single(l => l.BillingItemId == third.Id).UnitPrice.Should().Be(50m);
    }

    [Fact]
    public async Task ApplyMultipleOperationRuleAsync_SingleOperationsItem_LeavesFeeUnchanged()
    {
        var only = MakeBillingItem("30001", "T8");
        var lines = new List<InvoiceItem> { MakeLine(only, 500m, 400m) };

        await _calculator.ApplyMultipleOperationRuleAsync(lines, AccountTypeEnum.PrivatePatient);

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

        await _calculator.ApplyMultipleOperationRuleAsync(lines, AccountTypeEnum.PrivatePatient);

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

        var act = async () => await _calculator.ApplyMultipleOperationRuleAsync(lines, AccountTypeEnum.PrivatePatient);

        await act.Should().NotThrowAsync();
        lines[0].UnitPrice.Should().Be(100m);
    }

    [Fact]
    public async Task ApplyMultipleOperationRuleAsync_StandardAccountType_Subgroup12Excluded_EvenWithSiblingOperations()
    {
        // Ported from WorkSafe QLD's Multiple Operation Rule page — Subgroup 12 (amputations) is
        // excluded from the rule under standard Medicare too, not just WorkCover.
        var amputation = MakeBillingItem("44325", "T8", subGroup: "12");
        var other = MakeBillingItem("30001", "T8", subGroup: "1");
        var lines = new List<InvoiceItem>
        {
            MakeLine(amputation, 400m, 300m),
            MakeLine(other, 500m, 400m)
        };

        await _calculator.ApplyMultipleOperationRuleAsync(lines, AccountTypeEnum.PrivatePatient);

        lines.Single(l => l.BillingItemId == amputation.Id).UnitPrice.Should().Be(400m); // untouched
        lines.Single(l => l.BillingItemId == other.Id).UnitPrice.Should().Be(500m); // sole remaining item in its pool — no abatement either
    }

    [Fact]
    public async Task ApplyMultipleOperationRuleAsync_WorkCover_MixedGeneralAndOrthopaedic_BothPoolsProduce100PercentItem()
    {
        // WorkSafe QLD: "Where a medical practitioner performs both surgical and orthopaedic
        // procedures on the one occasion, each rule applies in its entirety... two items with
        // fees at 100%." Real item shapes: 30515 (Subgroup 1, general) + 30375 (Subgroup 1,
        // general) + 47528 (Subgroup 15, orthopaedic) + 49212 (Subgroup 15, orthopaedic).
        var generalHighest = MakeBillingItem("30515", "T8", subGroup: "1");
        var generalSecond = MakeBillingItem("30375", "T8", subGroup: "1");
        var orthoHighest = MakeBillingItem("47528", "T8", subGroup: "15");
        var orthoSecond = MakeBillingItem("49212", "T8", subGroup: "15");
        var lines = new List<InvoiceItem>
        {
            MakeLine(generalHighest, 843.05m, 632.30m),
            MakeLine(generalSecond, 800m, 600m),
            MakeLine(orthoHighest, 901.55m, 676.20m),
            MakeLine(orthoSecond, 281.85m, 211.40m)
        };

        await _calculator.ApplyMultipleOperationRuleAsync(lines, AccountTypeEnum.WorkCover);

        lines.Single(l => l.BillingItemId == generalHighest.Id).UnitPrice.Should().Be(843.05m); // 100% — general pool's highest
        lines.Single(l => l.BillingItemId == generalSecond.Id).UnitPrice.Should().Be(400.00m); // 50% of 800
        lines.Single(l => l.BillingItemId == orthoHighest.Id).UnitPrice.Should().Be(901.55m); // 100% — orthopaedic pool's own highest
        lines.Single(l => l.BillingItemId == orthoSecond.Id).UnitPrice.Should().Be(211.39m); // 75% of 281.85
    }

    [Fact]
    public async Task ApplyMultipleOperationRuleAsync_WorkCover_ThreeOrthopaedicItems_FlatSeventyFivePercent_NoTaper()
    {
        // WorkSafe QLD: orthopaedic/hand = "100% for the greatest, plus 75% for the next greatest,
        // plus 75% of EACH OTHER item" — flat 75% after the first, not a tapering 3rd tier.
        var first = MakeBillingItem("47528", "T8", subGroup: "15");
        var second = MakeBillingItem("49212", "T8", subGroup: "15");
        var third = MakeBillingItem("46300", "T8", subGroup: "14");
        var lines = new List<InvoiceItem>
        {
            MakeLine(first, 900m, 700m),
            MakeLine(second, 600m, 450m),
            MakeLine(third, 400m, 300m)
        };

        await _calculator.ApplyMultipleOperationRuleAsync(lines, AccountTypeEnum.WorkCover);

        lines.Single(l => l.BillingItemId == first.Id).UnitPrice.Should().Be(900m); // 100%
        lines.Single(l => l.BillingItemId == second.Id).UnitPrice.Should().Be(450m); // 75% of 600
        lines.Single(l => l.BillingItemId == third.Id).UnitPrice.Should().Be(300m); // 75% of 400 — same rate as 2nd, not a lower 3rd tier
    }

    [Fact]
    public async Task ApplyMultipleOperationRuleAsync_WorkCover_Subgroup12Excluded()
    {
        var amputation = MakeBillingItem("44325", "T8", subGroup: "12");
        var ortho1 = MakeBillingItem("47528", "T8", subGroup: "15");
        var ortho2 = MakeBillingItem("49212", "T8", subGroup: "15");
        var lines = new List<InvoiceItem>
        {
            MakeLine(amputation, 400m, 300m),
            MakeLine(ortho1, 900m, 700m),
            MakeLine(ortho2, 600m, 450m)
        };

        await _calculator.ApplyMultipleOperationRuleAsync(lines, AccountTypeEnum.WorkCover);

        lines.Single(l => l.BillingItemId == amputation.Id).UnitPrice.Should().Be(400m); // untouched
        lines.Single(l => l.BillingItemId == ortho1.Id).UnitPrice.Should().Be(900m);
        lines.Single(l => l.BillingItemId == ortho2.Id).UnitPrice.Should().Be(450m); // 75%, unaffected by the excluded amputation item
    }

    [Fact]
    public async Task ApplyMultipleOperationRuleAsync_WorkCover_UnrecognisedSubGroup_FallsBackToSurgicalPool()
    {
        var unknownSubGroup = MakeBillingItem("99999", "T8", subGroup: null);
        var general = MakeBillingItem("30001", "T8", subGroup: "1");
        var lines = new List<InvoiceItem>
        {
            MakeLine(unknownSubGroup, 500m, 400m),
            MakeLine(general, 300m, 250m)
        };

        await _calculator.ApplyMultipleOperationRuleAsync(lines, AccountTypeEnum.WorkCover);

        // Pooled with Surgical (100/50/25), not silently skipped or misclassified as Orthopaedic.
        lines.Single(l => l.BillingItemId == unknownSubGroup.Id).UnitPrice.Should().Be(500m);
        lines.Single(l => l.BillingItemId == general.Id).UnitPrice.Should().Be(150m);
    }
}

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

    private int CreateSchedule(string code = "SCHED")
    {
        var schedule = new FeeSchedule { Code = code, Description = code, RoundingType = RoundingTypeEnum.Exact };
        _context.FeeSchedules.Add(schedule);
        _context.SaveChanges();
        return schedule.Id;
    }

    private FeeScheduleItem CreateFeeScheduleItem(int feeScheduleId, BillingItem billingItem, decimal fee = 0,
        decimal? medicalGapPercent = null, decimal? percentageFromAssociatedItemFee = null, decimal? overLimitQuantityPlus = null)
    {
        var item = new FeeScheduleItem
        {
            FeeScheduleId = feeScheduleId,
            BillingItemId = billingItem.Id,
            Fee = fee,
            MedicalGapPercent = medicalGapPercent,
            PercentageFromAssociatedItemFee = percentageFromAssociatedItemFee,
            OverLimitQuantityPlus = overLimitQuantityPlus
        };
        _context.FeeScheduleItems.Add(item);
        _context.SaveChanges();
        return item;
    }

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
    public async Task ApplyDerivedFeesAsync_ExcisionMalignantTumour_UsesSameFormulaAsPercentageOfAssociatedItem()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.ExcisionMalignantTumour,
            AssociatedBillingItemId = _primaryItem.Id,
            Percentage = 30m
        });
        await _context.SaveChangesAsync();

        var primaryLine = MakeItem(_primaryItem, unitPrice: 500m, rebatePerUnit: 400m);
        var derivedLine = MakeItem(_derivedItem);
        var items = new List<InvoiceItem> { primaryLine, derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        derivedLine.UnitPrice.Should().Be(150m);
        derivedLine.RebatePerUnit.Should().Be(120m);
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_AssociatedItemNumbers_MatchesByRangeList()
    {
        var op1 = new BillingItem { ItemNumber = "100", Description = "Op 100", ScheduleFee = 0m, IsActive = true };
        var op2 = new BillingItem { ItemNumber = "101", Description = "Op 101", ScheduleFee = 0m, IsActive = true };
        var op3 = new BillingItem { ItemNumber = "102", Description = "Op 102", ScheduleFee = 0m, IsActive = true };
        var nonMatching = new BillingItem { ItemNumber = "999", Description = "Unrelated", ScheduleFee = 0m, IsActive = true };
        _context.BillingItems.AddRange(op1, op2, op3, nonMatching);
        await _context.SaveChangesAsync();

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.PercentageOfAssociatedItem,
            AssociatedItemNumbers = "100-102",
            Percentage = 10m
        });
        await _context.SaveChangesAsync();

        var items = new List<InvoiceItem>
        {
            MakeItem(op1, unitPrice: 100m, rebatePerUnit: 100m),
            MakeItem(op2, unitPrice: 200m, rebatePerUnit: 200m),
            MakeItem(op3, unitPrice: 300m, rebatePerUnit: 300m),
            MakeItem(nonMatching, unitPrice: 1000m, rebatePerUnit: 1000m),
            MakeItem(_derivedItem)
        };

        await _calculator.ApplyDerivedFeesAsync(items);

        items.Last().UnitPrice.Should().Be(60m); // 10% of (100+200+300), excluding the 1000 non-matching item
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_AssociatedGroup_SumsAllMatchingSiblings_ExcludingNonGroupLines()
    {
        // Worked example: three T8 operations abated by the Multiple Operation Rule to
        // $1,200 / $400 / $100 (already applied upstream — this test supplies the abated fees
        // directly). The real MBS item 51303 rule is 20% of the SUM of all eligible operations
        // ($1,700), not just the highest-fee one ($1,200).
        var op1 = new BillingItem { ItemNumber = "30515", Description = "Laparotomy", Group = "T8", ScheduleFee = 0m, IsActive = true };
        var op2 = new BillingItem { ItemNumber = "30375", Description = "Small bowel resection", Group = "T8", ScheduleFee = 0m, IsActive = true };
        var op3 = new BillingItem { ItemNumber = "30390", Description = "Haemostasis", Group = "T8", ScheduleFee = 0m, IsActive = true };
        var nonOp = new BillingItem { ItemNumber = "104", Description = "Consult", Group = "A3", ScheduleFee = 0m, IsActive = true };
        _context.BillingItems.AddRange(op1, op2, op3, nonOp);
        await _context.SaveChangesAsync();

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.PercentageOfAssociatedItem,
            AssociatedGroup = "T8",
            Percentage = 20m
        });
        await _context.SaveChangesAsync();

        var primary = MakeItem(op1, unitPrice: 1200m, rebatePerUnit: 900m);
        var second = MakeItem(op2, unitPrice: 400m, rebatePerUnit: 300m);
        var third = MakeItem(op3, unitPrice: 100m, rebatePerUnit: 75m);
        var consultLine = MakeItem(nonOp, unitPrice: 80m, rebatePerUnit: 80m); // must be excluded — Group A3, not T8
        var assistantLine = MakeItem(_derivedItem);
        var items = new List<InvoiceItem> { primary, second, third, consultLine, assistantLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        assistantLine.UnitPrice.Should().Be(340m); // 20% of (1200 + 400 + 100) = 20% of 1700
        assistantLine.RebatePerUnit.Should().Be(255m); // 20% of (900 + 300 + 75) = 20% of 1275
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_ProcedureDiscontinued_SumsPercentageOfEveryOtherLine_NoAssociationFilter()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.ProcedureDiscontinued,
            Percentage = 50m
        });
        await _context.SaveChangesAsync();

        var line1 = MakeItem(_primaryItem, unitPrice: 200m, rebatePerUnit: 160m);
        var other = new BillingItem { ItemNumber = "999", Description = "Other", ScheduleFee = 0m, IsActive = true };
        _context.BillingItems.Add(other);
        await _context.SaveChangesAsync();
        var line2 = MakeItem(other, unitPrice: 300m, rebatePerUnit: 240m);
        var derivedLine = MakeItem(_derivedItem);
        var items = new List<InvoiceItem> { line1, line2, derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        derivedLine.UnitPrice.Should().Be(250m); // 50% of (200+300)
        derivedLine.RebatePerUnit.Should().Be(200m); // 50% of (160+240)
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_CombinationOperations_SumsPercentageOfInvoiceTotal_ExcludingSelf()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.CombinationOperations,
            PercentageFromInvoiceTotal = 10m
        });
        await _context.SaveChangesAsync();

        var line1 = MakeItem(_primaryItem, unitPrice: 100m, rebatePerUnit: 100m);
        var other = new BillingItem { ItemNumber = "999", Description = "Other", ScheduleFee = 0m, IsActive = true };
        _context.BillingItems.Add(other);
        await _context.SaveChangesAsync();
        var line2 = MakeItem(other, unitPrice: 200m, rebatePerUnit: 200m);
        var derivedLine = MakeItem(_derivedItem);
        var items = new List<InvoiceItem> { line1, line2, derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        derivedLine.UnitPrice.Should().Be(30m); // 10% of (100+200)
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_AssistanceAnaesthesia_GatedOnBothGroupAAndB_ComputesSum()
    {
        var groupAItem = new BillingItem { ItemNumber = "25200", Description = "Assist anaesthesia", ScheduleFee = 0m, IsActive = true };
        var groupBItem = new BillingItem { ItemNumber = "23010", Description = "Anaesthesia base", ScheduleFee = 0m, IsActive = true };
        _context.BillingItems.AddRange(groupAItem, groupBItem);
        await _context.SaveChangesAsync();

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.AssistanceAnaesthesia,
            GroupAAssociatedItems = "25200-25205",
            GroupBAssociatedItems = "23010-24136",
            Percentage = 50m
        });
        await _context.SaveChangesAsync();

        var lineA = MakeItem(groupAItem, unitPrice: 100m, rebatePerUnit: 100m);
        var lineB = MakeItem(groupBItem, unitPrice: 200m, rebatePerUnit: 200m);
        var derivedLine = MakeItem(_derivedItem);
        var items = new List<InvoiceItem> { lineA, lineB, derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        derivedLine.UnitPrice.Should().Be(150m); // 50% of (100+200)
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_AssistanceAnaesthesia_MissingGroupB_ResultsInZeroFee()
    {
        var groupAItem = new BillingItem { ItemNumber = "25200", Description = "Assist anaesthesia", ScheduleFee = 0m, IsActive = true };
        _context.BillingItems.Add(groupAItem);
        await _context.SaveChangesAsync();

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.AssistanceAnaesthesia,
            GroupAAssociatedItems = "25200-25205",
            GroupBAssociatedItems = "23010-24136",
            Percentage = 50m
        });
        await _context.SaveChangesAsync();

        var lineA = MakeItem(groupAItem, unitPrice: 100m, rebatePerUnit: 100m);
        var derivedLine = MakeItem(_derivedItem, unitPrice: 999m, rebatePerUnit: 999m);
        var items = new List<InvoiceItem> { lineA, derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        derivedLine.UnitPrice.Should().Be(0m);
        derivedLine.RebatePerUnit.Should().Be(0m);
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_BasicUnits_GatedOnGroupA_SumsRawFees()
    {
        var groupAItem = new BillingItem { ItemNumber = "500", Description = "Group A item", ScheduleFee = 0m, IsActive = true };
        _context.BillingItems.Add(groupAItem);
        await _context.SaveChangesAsync();

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.BasicUnits,
            GroupAAssociatedItems = "500",
            CalculateFromFee = 10m
        });
        await _context.SaveChangesAsync();

        var lineA = MakeItem(groupAItem, unitPrice: 50m, rebatePerUnit: 40m);
        var derivedLine = MakeItem(_derivedItem);
        var items = new List<InvoiceItem> { lineA, derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        derivedLine.UnitPrice.Should().Be(60m); // 10 base + 50
        derivedLine.RebatePerUnit.Should().Be(50m); // 10 base + 40
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_BasicUnits_MissingGroupA_ResultsInZeroFee()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.BasicUnits,
            GroupAAssociatedItems = "500",
            CalculateFromFee = 10m
        });
        await _context.SaveChangesAsync();

        var derivedLine = MakeItem(_derivedItem, unitPrice: 999m, rebatePerUnit: 999m);
        var items = new List<InvoiceItem> { derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        derivedLine.UnitPrice.Should().Be(0m);
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_NumberOfPatientsSeen_PathA_UnderLimit_UsesCalculateFromFee()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.NumberOfPatientsSeen,
            CalculateFromFee = 100m,
            NumOfLimitPatient = 6,
            UnderNumOfLimitPatientPlusTotal = 31.50m,
            OverNumOfLimitPatientPlus = 2.50m
        });
        await _context.SaveChangesAsync();

        var derivedLine = MakeItem(_derivedItem, derivedQuantity: 3m);
        var items = new List<InvoiceItem> { derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        derivedLine.UnitPrice.Should().Be(110.50m); // 100 + 31.50/3
        derivedLine.RebatePerUnit.Should().Be(110.50m);
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_NumberOfPatientsSeen_PathA_OverLimit_UsesOverLimitPlus()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.NumberOfPatientsSeen,
            CalculateFromFee = 100m,
            NumOfLimitPatient = 6,
            UnderNumOfLimitPatientPlusTotal = 31.50m,
            OverNumOfLimitPatientPlus = 2.50m
        });
        await _context.SaveChangesAsync();

        var derivedLine = MakeItem(_derivedItem, derivedQuantity: 10m);
        var items = new List<InvoiceItem> { derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        derivedLine.UnitPrice.Should().Be(102.50m); // 100 + 2.50
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_NumberOfPatientsSeen_PathC_UsesAssociatedItemFeeViaSchedule()
    {
        var scheduleId = CreateSchedule();
        CreateFeeScheduleItem(scheduleId, _primaryItem, fee: 80m);

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.NumberOfPatientsSeen,
            AssociatedBillingItemId = _primaryItem.Id,
            NumOfLimitPatient = 6,
            UnderNumOfLimitPatientPlusTotal = 31.50m,
            OverNumOfLimitPatientPlus = 2.50m
        });
        await _context.SaveChangesAsync();

        var derivedLine = MakeItem(_derivedItem, derivedQuantity: 2m);
        var items = new List<InvoiceItem> { derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items, scheduleId);

        derivedLine.UnitPrice.Should().Be(95.75m); // 80 + 31.50/2
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_NumberOfPatientsSeen_PrecalculatedOverride_TakesPriorityOverFormula()
    {
        var scheduleId = CreateSchedule();
        var scheduleItem = CreateFeeScheduleItem(scheduleId, _derivedItem, fee: 0m);
        _context.DerivedItemRateCalculateds.Add(new DerivedItemRateCalculated { FeeScheduleItemId = scheduleItem.Id, OrderNum = 3, Fee = 999m });
        await _context.SaveChangesAsync();

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.NumberOfPatientsSeen,
            AssociatedBillingItemId = _primaryItem.Id,
            NumOfLimitPatient = 6,
            UnderNumOfLimitPatientPlusTotal = 31.50m,
            OverNumOfLimitPatientPlus = 2.50m
        });
        await _context.SaveChangesAsync();

        var derivedLine = MakeItem(_derivedItem, derivedQuantity: 3m);
        var items = new List<InvoiceItem> { derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items, scheduleId);

        derivedLine.UnitPrice.Should().Be(999m);
        derivedLine.RebatePerUnit.Should().Be(999m);
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_FieldQuantity_UnderLimit_BaseFeeUnchanged()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.FieldQuantity,
            AssociatedBillingItemId = _primaryItem.Id, // ScheduleFee 500, no schedule passed so falls back to MBS fee
            NumberOfLimitQuantity = 5m,
            OverLimitQuantityPlus = 20m
        });
        await _context.SaveChangesAsync();

        var derivedLine = MakeItem(_derivedItem, derivedQuantity: 3m);
        var items = new List<InvoiceItem> { derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        derivedLine.UnitPrice.Should().Be(500m); // under limit — base fee unchanged
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_FieldQuantity_OverLimit_AddsOverLimitPlusTimesExcess()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.FieldQuantity,
            AssociatedBillingItemId = _primaryItem.Id,
            NumberOfLimitQuantity = 5m,
            OverLimitQuantityPlus = 20m
        });
        await _context.SaveChangesAsync();

        var derivedLine = MakeItem(_derivedItem, derivedQuantity: 8m);
        var items = new List<InvoiceItem> { derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        derivedLine.UnitPrice.Should().Be(560m); // 500 + (8-5)*20
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_FieldQuantity_PrecalculatedOverride_TakesPriorityOverFormula()
    {
        var scheduleId = CreateSchedule();
        var scheduleItem = CreateFeeScheduleItem(scheduleId, _derivedItem, fee: 0m);
        _context.DerivedItemRateCalculateds.Add(new DerivedItemRateCalculated { FeeScheduleItemId = scheduleItem.Id, OrderNum = 8, Fee = 777m });
        await _context.SaveChangesAsync();

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.FieldQuantity,
            AssociatedBillingItemId = _primaryItem.Id,
            NumberOfLimitQuantity = 5m,
            OverLimitQuantityPlus = 20m
        });
        await _context.SaveChangesAsync();

        var derivedLine = MakeItem(_derivedItem, derivedQuantity: 8m);
        var items = new List<InvoiceItem> { derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items, scheduleId);

        derivedLine.UnitPrice.Should().Be(777m);
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_TimeDuration_AlwaysAddsQuantityTimesRate_NoThreshold()
    {
        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.TimeDuration,
            AssociatedBillingItemId = _primaryItem.Id,
            OverLimitQuantityPlus = 3m
        });
        await _context.SaveChangesAsync();

        var derivedLine = MakeItem(_derivedItem, derivedQuantity: 30m);
        var items = new List<InvoiceItem> { derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items);

        // 500 (base) + 30*3 = 590 — unlike FieldQuantity, no limit subtraction at all.
        derivedLine.UnitPrice.Should().Be(590m);
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_ScheduleOverride_PercentageFromAssociatedItemFee_ReplacesConfigDefault()
    {
        var scheduleId = CreateSchedule();
        CreateFeeScheduleItem(scheduleId, _derivedItem, percentageFromAssociatedItemFee: 10m);

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.PercentageOfAssociatedItem,
            AssociatedBillingItemId = _primaryItem.Id,
            Percentage = 20m
        });
        await _context.SaveChangesAsync();

        var primaryLine = MakeItem(_primaryItem, unitPrice: 500m, rebatePerUnit: 400m);
        var derivedLine = MakeItem(_derivedItem);
        var items = new List<InvoiceItem> { primaryLine, derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items, scheduleId);

        derivedLine.UnitPrice.Should().Be(50m); // 10% (schedule override), not 20% (config default)
    }

    [Fact]
    public async Task ApplyDerivedFeesAsync_ScheduleOverride_MedicalGapPercent_AppliesToFeeOnly_NotRebate()
    {
        var scheduleId = CreateSchedule();
        CreateFeeScheduleItem(scheduleId, _derivedItem, medicalGapPercent: 50m);

        _context.DerivedItemConfigs.Add(new DerivedItemConfig
        {
            BillingItemId = _derivedItem.Id,
            CalculationType = DerivedCalculationType.PercentageOfAssociatedItem,
            AssociatedBillingItemId = _primaryItem.Id,
            Percentage = 20m
        });
        await _context.SaveChangesAsync();

        var primaryLine = MakeItem(_primaryItem, unitPrice: 500m, rebatePerUnit: 400m);
        var derivedLine = MakeItem(_derivedItem);
        var items = new List<InvoiceItem> { primaryLine, derivedLine };

        await _calculator.ApplyDerivedFeesAsync(items, scheduleId);

        derivedLine.UnitPrice.Should().Be(50m); // 20% of 500 = 100, then x50% gap = 50
        derivedLine.RebatePerUnit.Should().Be(80m); // 20% of 400 = 80, gap never touches rebate
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

using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Services;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Services;

public class BillingCalculatorTests : IDisposable
{
    private readonly BillingCalculator _calculator;
    private readonly AppDbContext _context;
    private readonly int _billingItemId;

    private const int ClinicId = 1;

    public BillingCalculatorTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _calculator = new BillingCalculator(_context);

        var billingItem = new BillingItem { ItemNumber = "23", Description = "Standard GP consultation", ScheduleFee = 41.40m, Benefit100 = 41.40m, IsActive = true };
        _context.BillingItems.Add(billingItem);
        _context.SaveChanges();
        _billingItemId = billingItem.Id;
    }

    public void Dispose() => _context.Dispose();

    private int CreateSchedule(string code, decimal fee, int? healthFundId = null, int? feeTableId = null, RoundingTypeEnum rounding = RoundingTypeEnum.Exact)
    {
        var schedule = new FeeSchedule { Code = code, Description = code, RoundingType = rounding, HealthFundId = healthFundId, FeeTableId = feeTableId };
        _context.FeeSchedules.Add(schedule);
        _context.SaveChanges();

        if (fee >= 0)
        {
            _context.FeeScheduleItems.Add(new FeeScheduleItem { FeeScheduleId = schedule.Id, BillingItemId = _billingItemId, Fee = fee });
            _context.SaveChanges();
        }

        return schedule.Id;
    }

    private void MapAccountType(AccountTypeEnum accountType, int feeScheduleId) =>
        _context.AccountTypeFeeScheduleMappings.Add(new AccountTypeFeeScheduleMapping { ClinicId = ClinicId, AccountType = accountType, FeeScheduleId = feeScheduleId });

    [Fact]
    public async Task ResolveLineAsync_BulkBill_FeeEqualsRebate_FromMappedSchedule()
    {
        var scheduleId = CreateSchedule("BBGP", 39.75m);
        MapAccountType(AccountTypeEnum.BulkBill, scheduleId);
        _context.SaveChanges();

        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.BulkBill, ProviderServiceType.GeneralPractitioner, PlaceOfServiceEnum.Rooms, _billingItemId, null);

        result.Fee.Should().Be(39.75m);
        result.RebatePerUnit.Should().Be(39.75m);
    }

    [Fact]
    public async Task ResolveLineAsync_Private_UsesAccountTypeMapping_And_Benefit100Rebate()
    {
        var privateScheduleId = CreateSchedule("PRIV", 62.10m);
        MapAccountType(AccountTypeEnum.PrivatePatient, privateScheduleId);
        _context.SaveChanges();

        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.PrivatePatient, ProviderServiceType.GeneralPractitioner, PlaceOfServiceEnum.Rooms, _billingItemId, null);

        result.Fee.Should().Be(62.10m);
        result.RebatePerUnit.Should().Be(41.40m);
    }

    [Fact]
    public async Task ResolveLineAsync_Private_Gp_Rooms_UsesBenefit100()
    {
        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.PrivatePatient, ProviderServiceType.GeneralPractitioner, PlaceOfServiceEnum.Rooms, _billingItemId, null);

        result.RebatePerUnit.Should().Be(41.40m); // 100% — GP attendance item, Benefit100 populated
    }

    [Fact]
    public async Task ResolveLineAsync_Private_Specialist_Rooms_Uses85Percent()
    {
        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.PrivatePatient, ProviderServiceType.Specialist, PlaceOfServiceEnum.Rooms, _billingItemId, null);

        result.RebatePerUnit.Should().Be(41.40m * 0.85m);
    }

    [Fact]
    public async Task ResolveLineAsync_Private_Hospital_Uses75Percent_RegardlessOfProviderType()
    {
        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.PrivatePatient, ProviderServiceType.GeneralPractitioner, PlaceOfServiceEnum.Hospital, _billingItemId, null);

        result.RebatePerUnit.Should().Be(41.40m * 0.75m); // hospital priority overrides GP/100%
    }

    [Fact]
    public async Task ResolveLineAsync_Private_GpItemWithNoBenefit100_FallsBackTo85Percent()
    {
        var proceduralItem = new BillingItem { ItemNumber = "30001", Description = "GP procedure", ScheduleFee = 100.00m, Benefit100 = null, IsActive = true };
        _context.BillingItems.Add(proceduralItem);
        await _context.SaveChangesAsync();

        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.PrivatePatient, ProviderServiceType.GeneralPractitioner, PlaceOfServiceEnum.Rooms, proceduralItem.Id, null);

        result.RebatePerUnit.Should().Be(85.00m);
    }

    [Fact]
    public async Task ResolveLineAsync_FundSpecificSchedule_TakesPrecedenceOverAccountTypeMapping()
    {
        var genericScheduleId = CreateSchedule("PRIV", 62.10m);
        MapAccountType(AccountTypeEnum.PrivatePatient, genericScheduleId);

        var healthFund = new HealthFund { Code = "HBF", Name = "HBF Health" };
        _context.HealthFunds.Add(healthFund);
        _context.SaveChanges();

        var fundScheduleId = CreateSchedule("HBF-SCHED", 55.00m, healthFundId: healthFund.Id);
        _context.SaveChanges();

        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.PrivatePatient, ProviderServiceType.GeneralPractitioner, PlaceOfServiceEnum.Rooms, _billingItemId, null, healthFund.Id);

        result.Fee.Should().Be(55.00m);
    }

    [Fact]
    public async Task ResolveLineAsync_FundScheduleMissingItem_FallsBackThroughFeeTableToParent()
    {
        var healthFund = new HealthFund { Code = "HBF", Name = "HBF Health" };
        _context.HealthFunds.Add(healthFund);
        _context.SaveChanges();

        var parentScheduleId = CreateSchedule("AHSA-QLD", 70.00m);

        // Child fund schedule inherits from the parent but has no item of its own.
        var childSchedule = new FeeSchedule { Code = "HBF-QLD", Description = "HBF", HealthFundId = healthFund.Id, FeeTableId = parentScheduleId, RoundingType = RoundingTypeEnum.Exact };
        _context.FeeSchedules.Add(childSchedule);
        _context.SaveChanges();

        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.PrivatePatient, ProviderServiceType.GeneralPractitioner, PlaceOfServiceEnum.Rooms, _billingItemId, null, healthFund.Id);

        result.Fee.Should().Be(70.00m);
    }

    [Fact]
    public async Task ResolveLineAsync_MultipleFundSchedulesMatch_FallsBackToAccountTypeMapping()
    {
        var genericScheduleId = CreateSchedule("PRIV", 62.10m);
        MapAccountType(AccountTypeEnum.PrivatePatient, genericScheduleId);

        var healthFund = new HealthFund { Code = "HBF", Name = "HBF Health" };
        _context.HealthFunds.Add(healthFund);
        _context.SaveChanges();

        // Two active schedules tagged with the same fund — ambiguous, must not guess.
        CreateSchedule("HBF-A", 50.00m, healthFundId: healthFund.Id);
        CreateSchedule("HBF-B", 51.00m, healthFundId: healthFund.Id);
        _context.SaveChanges();

        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.PrivatePatient, ProviderServiceType.GeneralPractitioner, PlaceOfServiceEnum.Rooms, _billingItemId, null, healthFund.Id);

        result.Fee.Should().Be(62.10m);
    }

    [Fact]
    public async Task ResolveLineAsync_Veteran_FeeEqualsRebate_FromVagpMatrix_IsolatedFromBulkBill()
    {
        CreateSchedule("BBGP", 999.99m); // different value, to prove DVA doesn't fall into the BB matrix
        CreateSchedule("VAGP", 45.20m);
        _context.SaveChanges();

        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.Veteran, ProviderServiceType.GeneralPractitioner, PlaceOfServiceEnum.Rooms, _billingItemId, null);

        result.Fee.Should().Be(45.20m);
        result.RebatePerUnit.Should().Be(45.20m);
    }

    [Fact]
    public async Task ResolveLineAsync_Veteran_SpecialistHospital_UsesVasi()
    {
        CreateSchedule("VAGP", 40.00m);
        CreateSchedule("VASO", 60.00m);
        CreateSchedule("VASI", 80.00m);
        _context.SaveChanges();

        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.Veteran, ProviderServiceType.Specialist, PlaceOfServiceEnum.Hospital, _billingItemId, null);

        result.Fee.Should().Be(80.00m);
    }

    [Fact]
    public async Task ResolveLineAsync_Imc_UsesFundSchedule_LikePrivate()
    {
        var genericScheduleId = CreateSchedule("PRIV", 62.10m);
        MapAccountType(AccountTypeEnum.Imc, genericScheduleId);

        var healthFund = new HealthFund { Code = "MED", Name = "Medibank" };
        _context.HealthFunds.Add(healthFund);
        _context.SaveChanges();

        CreateSchedule("MED-SCHED", 58.00m, healthFundId: healthFund.Id);
        _context.SaveChanges();

        var result = await _calculator.ResolveLineAsync(ClinicId, AccountTypeEnum.Imc, ProviderServiceType.GeneralPractitioner, PlaceOfServiceEnum.Rooms, _billingItemId, null, healthFund.Id);

        result.Fee.Should().Be(58.00m);
    }
}

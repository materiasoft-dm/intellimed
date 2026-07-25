using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Resolves fee / rebate / GST for an invoice line, mirroring legacy Pracnet's IRebateCalculator.
/// The charged fee comes from the account-type-mapped fee schedule (falling back to the MBS
/// ScheduleFee); the rebate is the bulk-bill-equivalent fee resolved from the BBGP/BBO/BBI schedules
/// by provider service-type + place of service.
/// </summary>
public class BillingCalculator : IBillingCalculator
{
    private readonly AppDbContext _context;

    // Reserved bulk-bill schedule codes, matching legacy's string-based lookup.
    private const string BbGpCode = "BBGP";   // GP, rooms
    private const string BbOutCode = "BBO";   // Specialist/other, rooms
    private const string BbInCode = "BBI";    // Hospital (in-patient)

    public BillingCalculator(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ResolveLineResult> ResolveLineAsync(
        int clinicId,
        AccountTypeEnum accountType,
        ProviderServiceType providerServiceType,
        PlaceOfServiceEnum placeOfService,
        int billingItemId,
        DateTime? serviceDate)
    {
        var billingItem = await _context.BillingItems.FirstOrDefaultAsync(b => b.Id == billingItemId);
        var result = new ResolveLineResult
        {
            Description = billingItem?.Description ?? string.Empty
        };
        if (billingItem == null)
            return result;

        // Resolve the charged fee schedule for this account type (per clinic).
        var mapping = await _context.AccountTypeFeeScheduleMappings
            .FirstOrDefaultAsync(m => m.ClinicId == clinicId && m.AccountType == accountType);

        var chargedRounding = RoundingTypeEnum.Exact;
        decimal? chargedScheduleFee = null;
        if (mapping != null)
        {
            var schedule = await _context.FeeSchedules.FirstOrDefaultAsync(f => f.Id == mapping.FeeScheduleId);
            if (schedule != null)
            {
                chargedRounding = schedule.RoundingType;
                chargedScheduleFee = await FeeForScheduleAsync(schedule.Id, billingItemId);
            }
        }

        if (accountType == AccountTypeEnum.BulkBill)
        {
            // Bulk bill: fee = the (BB) schedule item fee, fallback to the MBS 100% benefit. Rebate = fee (100%).
            var fee = chargedScheduleFee ?? billingItem.Benefit100 ?? billingItem.ScheduleFee;
            fee = BillingMath.ApplyScheduleRounding(fee, chargedRounding);
            result.Fee = fee;
            result.RebatePerUnit = fee;
            return result;
        }

        // Private / other: fee from the mapped schedule (fallback to the MBS schedule fee).
        var chargedFee = chargedScheduleFee ?? billingItem.ScheduleFee;
        result.Fee = BillingMath.ApplyScheduleRounding(chargedFee, chargedRounding);

        // Rebate = bulk-bill-equivalent fee, resolved by service-type + place of service.
        var rebate = await ResolveBulkBillEquivalentAsync(providerServiceType, placeOfService, billingItemId)
                     ?? billingItem.Benefit100
                     ?? 0m;
        result.RebatePerUnit = BillingMath.ApplyScheduleRounding(rebate, chargedRounding);

        return result;
    }

    /// <summary>Legacy BBGP/BBO/BBI selection matrix (Private_RebateCalculator).</summary>
    private async Task<decimal?> ResolveBulkBillEquivalentAsync(
        ProviderServiceType providerServiceType, PlaceOfServiceEnum placeOfService, int billingItemId)
    {
        if (providerServiceType == ProviderServiceType.GeneralPractitioner)
        {
            // GP: prefer BBGP, then fall back to BBO (rooms) / BBI (hospital).
            return await FeeForCodeAsync(BbGpCode, billingItemId)
                   ?? await FeeForCodeAsync(placeOfService == PlaceOfServiceEnum.Hospital ? BbInCode : BbOutCode, billingItemId);
        }

        // Specialist / Pathology: BBO (rooms) or BBI (hospital), falling back to BBGP.
        return await FeeForCodeAsync(placeOfService == PlaceOfServiceEnum.Hospital ? BbInCode : BbOutCode, billingItemId)
               ?? await FeeForCodeAsync(BbGpCode, billingItemId);
    }

    private async Task<decimal?> FeeForCodeAsync(string scheduleCode, int billingItemId)
    {
        var scheduleId = await _context.FeeSchedules
            .Where(f => !f.IsArchived && f.Code == scheduleCode)
            .Select(f => (int?)f.Id)
            .FirstOrDefaultAsync();

        return scheduleId == null ? null : await FeeForScheduleAsync(scheduleId.Value, billingItemId);
    }

    private async Task<decimal?> FeeForScheduleAsync(int scheduleId, int billingItemId)
    {
        return await _context.FeeScheduleItems
            .Where(i => i.FeeScheduleId == scheduleId && i.BillingItemId == billingItemId)
            .Select(i => (decimal?)i.Fee)
            .FirstOrDefaultAsync();
    }
}

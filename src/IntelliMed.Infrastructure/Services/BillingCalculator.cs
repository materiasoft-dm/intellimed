using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Resolves fee / rebate / GST for an invoice line, mirroring legacy Pracnet's IRebateCalculator.
/// The charged fee comes from a fund-specific fee schedule when the client belongs to one
/// (falling back to the clinic's generic account-type mapping, then the MBS ScheduleFee), with
/// FeeTableId parent-schedule inheritance (e.g. a health fund borrowing from its state's gap-cover
/// schedule) resolved along the way. BulkBill and DVA (Veteran) are both fee=rebate, no-gap paths;
/// everything else's rebate follows the MBS Medicare benefit percentage rule (75%/85%/100% of the
/// Schedule Fee — see ResolveMedicareBenefit).
/// </summary>
public class BillingCalculator : IBillingCalculator
{
    private readonly AppDbContext _context;

    // Reserved DVA schedule codes. Unlike BBGP/BBO/BBI (a bulk-bill-equivalent lookup used to
    // compute a private patient's gap), these are DVA's own fixed, published fees — authoritative
    // in their own right, with no gap computed against them (fee = rebate, like BulkBill).
    private const string DvaGpCode = "VAGP";
    private const string DvaOutCode = "VASO"; // Specialist/pathology, rooms
    private const string DvaInCode = "VASI";  // Specialist/pathology, hospital

    // Bounds the FeeTableId parent-schedule walk (e.g. a health fund inheriting from a state AGC
    // schedule) so a misconfigured cycle can't loop forever.
    private const int MaxParentScheduleHops = 5;

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
        DateTime? serviceDate,
        int? healthFundId = null)
    {
        var billingItem = await _context.BillingItems.FirstOrDefaultAsync(b => b.Id == billingItemId);
        var result = new ResolveLineResult
        {
            Description = billingItem?.Description ?? string.Empty
        };
        if (billingItem == null)
            return result;

        // Resolve the charged fee schedule: a fund-specific schedule (matched by the client's
        // HealthFundId) takes precedence over the generic per-clinic account-type mapping.
        var scheduleId = await ResolveChargedFeeScheduleIdAsync(clinicId, accountType, healthFundId);

        var chargedRounding = RoundingTypeEnum.Exact;
        decimal? chargedScheduleFee = null;
        if (scheduleId.HasValue)
        {
            var (fee, rounding) = await FeeForScheduleWithParentFallbackAsync(scheduleId.Value, billingItemId);
            chargedScheduleFee = fee;
            chargedRounding = rounding;
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

        if (accountType == AccountTypeEnum.Veteran)
        {
            // DVA: its own published fee is authoritative — no bulk-bill-equivalent gap is computed
            // against it (mirrors legacy excluding DVA's reserved codes from the private-gap fallback).
            var fee = chargedScheduleFee
                      ?? await ResolveDvaEquivalentAsync(providerServiceType, placeOfService, billingItemId)
                      ?? billingItem.Benefit100
                      ?? billingItem.ScheduleFee;
            fee = BillingMath.ApplyScheduleRounding(fee, chargedRounding);
            result.Fee = fee;
            result.RebatePerUnit = fee;
            return result;
        }

        // Private / other (including IMC, resolved above via the same fund-matched schedule as any
        // other health-fund client): fee from the mapped schedule (fallback to the MBS schedule fee).
        var chargedFee = chargedScheduleFee ?? billingItem.ScheduleFee;
        result.Fee = BillingMath.ApplyScheduleRounding(chargedFee, chargedRounding);

        // Rebate = MBS Medicare benefit percentage rule.
        var rebate = ResolveMedicareBenefit(billingItem, providerServiceType, placeOfService);
        result.RebatePerUnit = BillingMath.ApplyScheduleRounding(rebate, chargedRounding);

        return result;
    }

    public Task<int?> ResolveFeeScheduleIdAsync(int clinicId, AccountTypeEnum accountType, int? healthFundId = null) =>
        ResolveChargedFeeScheduleIdAsync(clinicId, accountType, healthFundId);

    /// <summary>
    /// Picks the charged fee schedule: if the client belongs to a health fund and exactly one active
    /// schedule is tagged with that fund, it wins (mirrors legacy's ParticipantId-match precedence,
    /// including its "ambiguous match falls through rather than guessing" guard). Otherwise falls
    /// back to the clinic's generic per-account-type mapping.
    /// </summary>
    private async Task<int?> ResolveChargedFeeScheduleIdAsync(int clinicId, AccountTypeEnum accountType, int? healthFundId)
    {
        if (healthFundId.HasValue)
        {
            var fundScheduleIds = await _context.FeeSchedules
                .Where(f => !f.IsArchived && f.HealthFundId == healthFundId.Value)
                .Select(f => f.Id)
                .ToListAsync();

            if (fundScheduleIds.Count == 1)
                return fundScheduleIds[0];
        }

        var mapping = await _context.AccountTypeFeeScheduleMappings
            .FirstOrDefaultAsync(m => m.ClinicId == clinicId && m.AccountType == accountType);
        return mapping?.FeeScheduleId;
    }

    /// <summary>
    /// Looks up an item's fee on the given schedule; if missing, walks up FeeTableId (the schedule's
    /// parent, e.g. a health fund inheriting from its state's gap-cover schedule) until a fee is found
    /// or the chain runs out. Returns the fee together with the RoundingType of whichever schedule
    /// actually supplied it (or the original schedule's, if nothing was found anywhere in the chain).
    /// </summary>
    private async Task<(decimal? Fee, RoundingTypeEnum Rounding)> FeeForScheduleWithParentFallbackAsync(int scheduleId, int billingItemId)
    {
        int? currentScheduleId = scheduleId;
        for (var hop = 0; hop < MaxParentScheduleHops && currentScheduleId.HasValue; hop++)
        {
            var schedule = await _context.FeeSchedules.FirstOrDefaultAsync(f => f.Id == currentScheduleId.Value);
            if (schedule == null) break;

            var fee = await FeeForScheduleAsync(schedule.Id, billingItemId);
            if (fee.HasValue)
                return (fee, schedule.RoundingType);

            currentScheduleId = schedule.FeeTableId;
        }

        var originalRounding = await _context.FeeSchedules
            .Where(f => f.Id == scheduleId)
            .Select(f => (RoundingTypeEnum?)f.RoundingType)
            .FirstOrDefaultAsync();
        return (null, originalRounding ?? RoundingTypeEnum.Exact);
    }

    /// <summary>DVA's own reserved-code matrix — same GP/Specialist x Rooms/Hospital shape as the bulk-bill matrix.</summary>
    private async Task<decimal?> ResolveDvaEquivalentAsync(
        ProviderServiceType providerServiceType, PlaceOfServiceEnum placeOfService, int billingItemId)
    {
        if (providerServiceType == ProviderServiceType.GeneralPractitioner)
        {
            return await FeeForCodeAsync(DvaGpCode, billingItemId)
                   ?? await FeeForCodeAsync(placeOfService == PlaceOfServiceEnum.Hospital ? DvaInCode : DvaOutCode, billingItemId);
        }

        return await FeeForCodeAsync(placeOfService == PlaceOfServiceEnum.Hospital ? DvaInCode : DvaOutCode, billingItemId)
               ?? await FeeForCodeAsync(DvaGpCode, billingItemId);
    }

    /// <summary>
    /// MBS benefit percentage rule: 75% of the Schedule Fee for any service during an admitted hospital
    /// episode (takes priority over provider type); otherwise 100% for GP attendance items that MBS
    /// marks as 100%-eligible (Benefit100 populated), 85% of the Schedule Fee for everything else in
    /// the community. The MBS feed doesn't publish separate 85%/75% amounts — they're a flat percentage
    /// of ScheduleFee, not a lookup (unlike Benefit100, which mbsonline publishes per-item).
    /// </summary>
    private static decimal ResolveMedicareBenefit(BillingItem billingItem, ProviderServiceType providerServiceType, PlaceOfServiceEnum placeOfService)
    {
        if (placeOfService == PlaceOfServiceEnum.Hospital)
            return billingItem.ScheduleFee * 0.75m;

        if (providerServiceType == ProviderServiceType.GeneralPractitioner && billingItem.Benefit100.HasValue)
            return billingItem.Benefit100.Value;

        return billingItem.ScheduleFee * 0.85m;
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

using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Applies derived-item fee formulas as a post-pass over an invoice's line items — run after every
/// line has already been resolved normally by IBillingCalculator (and abated by
/// IMultipleOperationRuleCalculator, if applicable), since most formulas need a sibling line's
/// already-resolved fee as input.
///
/// Ported field-for-field from legacy Pracnet's DerivedFeeCalculator (Abaki.Billing.Bll), ground-truth
/// read from the real source rather than guessed. Two legacy strategies (AdditionalFoetusTested,
/// OriginalAmputationFee) are omitted — they are empty method bodies in legacy itself, never actually
/// implemented there either. One legacy fallback (OtherDerivedItemInfo/CheckOtherDerivedFeeNumberOfPatientsSeen,
/// a health-fund-schedule-specific RMFS-prefix special case) is deliberately not ported — FeeScheduleItem's
/// per-schedule overrides already cover "this schedule has its own values" for the mainline formula.
///
/// Deliberate simplifications vs. the legacy two-pass system (legacy recomputes each derived item twice,
/// once against the bulk-bill schedule for the rebate, once against the real schedule for the fee — two
/// different fee-rate GUIDs feeding two different lookups): (1) UnitPrice and RebatePerUnit are computed
/// in one pass using parallel sums, matching this codebase's already-established single-pass dual-field
/// pattern (see the assistant-at-surgery worked example this mirrors). (2) The schedule-level gap percent
/// (MedicalGapPercent) only ever adjusts UnitPrice, never RebatePerUnit — legacy's rebate pass always runs
/// against the bulk-bill schedule (IsBulkBill is true by construction there), which forces gapPercent to 0
/// for that pass every time; the fee pass is the only one where the invoice's own schedule ever supplies a
/// non-zero gap. (3) Legacy's PercentageFromAssociatedItems/ExcisionMalignantTumour/CombinationOperations
/// re-multiply the gap percent on every matching loop iteration (DerivedFeeCalculator.cs:135-138) rather
/// than once after the sum — every other legacy strategy applies it once at the end, so this reads as an
/// accidental loop bug rather than a deliberate rule; we apply it once at the end everywhere for
/// consistency.
/// </summary>
public class DerivedFeeCalculator : IDerivedFeeCalculator
{
    private readonly AppDbContext _context;

    public DerivedFeeCalculator(AppDbContext context)
    {
        _context = context;
    }

    private sealed record BillingItemInfo(int Id, string ItemNumber, string? Group, decimal ScheduleFee);

    public async Task ApplyDerivedFeesAsync(ICollection<InvoiceItem> items, int? feeScheduleId = null)
    {
        var billingItemIds = items
            .Where(i => i.BillingItemId.HasValue)
            .Select(i => i.BillingItemId!.Value)
            .Distinct()
            .ToList();
        if (billingItemIds.Count == 0) return;

        var configs = await _context.DerivedItemConfigs
            .Where(c => c.IsActive && billingItemIds.Contains(c.BillingItemId))
            .ToDictionaryAsync(c => c.BillingItemId);
        if (configs.Count == 0) return;

        var billingItemsById = await _context.BillingItems
            .Where(b => billingItemIds.Contains(b.Id))
            .Select(b => new BillingItemInfo(b.Id, b.ItemNumber, b.Group, b.ScheduleFee))
            .ToDictionaryAsync(b => b.Id);

        var overridesByBillingItemId = feeScheduleId.HasValue
            ? await _context.FeeScheduleItems
                .Where(f => f.FeeScheduleId == feeScheduleId.Value && configs.Keys.Contains(f.BillingItemId))
                .Include(f => f.DerivedItemRateCalculateds)
                .ToDictionaryAsync(f => f.BillingItemId)
            : new Dictionary<int, FeeScheduleItem>();

        foreach (var item in items)
        {
            if (!item.BillingItemId.HasValue || !configs.TryGetValue(item.BillingItemId.Value, out var config))
                continue;

            overridesByBillingItemId.TryGetValue(item.BillingItemId.Value, out var scheduleOverride);

            switch (config.CalculationType)
            {
                case DerivedCalculationType.PercentageOfAssociatedItem:
                case DerivedCalculationType.ExcisionMalignantTumour:
                    ApplyPercentageOfAssociated(item, config, items, billingItemsById, scheduleOverride, item2 => MatchesAssociation(config, item2));
                    break;

                case DerivedCalculationType.ProcedureDiscontinued:
                    ApplySumOfOthers(item, config.Percentage, items);
                    break;

                case DerivedCalculationType.CombinationOperations:
                    ApplyPercentageOfAssociated(item, config, items, billingItemsById, scheduleOverride, _ => true, config.PercentageFromInvoiceTotal, excludeUnmatched: false);
                    break;

                case DerivedCalculationType.AssistanceAnaesthesia:
                    ApplyAssistanceAnaesthesia(item, config, items, billingItemsById, scheduleOverride);
                    break;

                case DerivedCalculationType.BasicUnits:
                    ApplyBasicUnits(item, config, items, billingItemsById);
                    break;

                case DerivedCalculationType.NumberOfPatientsSeen:
                    await ApplyNumberOfPatientsSeenAsync(item, config, feeScheduleId, scheduleOverride);
                    break;

                case DerivedCalculationType.FieldQuantity:
                    await ApplyFieldQuantityAsync(item, config, feeScheduleId, scheduleOverride);
                    break;

                case DerivedCalculationType.TimeDuration:
                    await ApplyTimeDurationAsync(item, config, feeScheduleId, scheduleOverride);
                    break;
            }
        }
    }

    private static bool MatchesAssociation(DerivedItemConfig config, BillingItemInfo candidate)
    {
        if (config.AssociatedBillingItemId.HasValue)
            return candidate.Id == config.AssociatedBillingItemId.Value;
        if (!string.IsNullOrEmpty(config.AssociatedGroup))
            return candidate.Group == config.AssociatedGroup;
        if (!string.IsNullOrEmpty(config.AssociatedItemNumbers))
            return ItemNumberRangeMatcher.Parse(config.AssociatedItemNumbers).Contains(candidate.ItemNumber);
        return false;
    }

    /// <summary>
    /// Sums (percentage x siblingFee)/100 over every sibling line matching a predicate, then applies the
    /// fee-schedule override/gap either-or (PercentageOfAssociatedItem, ExcisionMalignantTumour,
    /// AssistanceAnaesthesia, CombinationOperations — all the same shape, different match predicates and
    /// percentage source).
    /// </summary>
    private static void ApplyPercentageOfAssociated(
        InvoiceItem item, DerivedItemConfig config, ICollection<InvoiceItem> items,
        IReadOnlyDictionary<int, BillingItemInfo> billingItemsById, FeeScheduleItem? scheduleOverride,
        Func<BillingItemInfo, bool> matches, decimal? percentageOverrideSource = null, bool excludeUnmatched = true)
    {
        var percentage = ResolveConfigPercentage(percentageOverrideSource ?? config.Percentage, scheduleOverride, out var gapPercent);

        decimal fee = 0, rebate = 0;
        var any = false;
        foreach (var sibling in items)
        {
            if (sibling == item || !sibling.BillingItemId.HasValue) continue;
            if (!billingItemsById.TryGetValue(sibling.BillingItemId.Value, out var info)) continue;
            if (excludeUnmatched && !matches(info)) continue;

            fee += percentage / 100m * sibling.UnitPrice;
            rebate += percentage / 100m * sibling.RebatePerUnit;
            any = true;
        }

        if (!any) return;

        if (gapPercent.HasValue)
            fee *= gapPercent.Value / 100m;

        item.UnitPrice = fee;
        item.RebatePerUnit = rebate;
    }

    /// <summary>ProcedureDiscontinued: sum (percentage x siblingFee)/100 over every OTHER sibling line, no association filter, no schedule override/gap.</summary>
    private static void ApplySumOfOthers(InvoiceItem item, decimal? percentage, ICollection<InvoiceItem> items)
    {
        if (!percentage.HasValue || percentage.Value <= 0) return;

        decimal fee = 0, rebate = 0;
        foreach (var sibling in items)
        {
            if (sibling == item) continue;
            fee += percentage.Value / 100m * sibling.UnitPrice;
            rebate += percentage.Value / 100m * sibling.RebatePerUnit;
        }

        item.UnitPrice = fee;
        item.RebatePerUnit = rebate;
    }

    /// <summary>Gated on both Group A and Group B having a matching sibling (else fee = 0); when open, sums over every sibling matching any of Groups A-D.</summary>
    private static void ApplyAssistanceAnaesthesia(
        InvoiceItem item, DerivedItemConfig config, ICollection<InvoiceItem> items,
        IReadOnlyDictionary<int, BillingItemInfo> billingItemsById, FeeScheduleItem? scheduleOverride)
    {
        var groupA = ItemNumberRangeMatcher.Parse(config.GroupAAssociatedItems);
        var groupB = ItemNumberRangeMatcher.Parse(config.GroupBAssociatedItems);
        var groupC = ItemNumberRangeMatcher.Parse(config.GroupCAssociatedItems);
        var groupD = ItemNumberRangeMatcher.Parse(config.GroupDAssociatedItems);

        bool hasA = false, hasB = false;
        foreach (var sibling in items)
        {
            if (sibling == item || !sibling.BillingItemId.HasValue) continue;
            if (!billingItemsById.TryGetValue(sibling.BillingItemId.Value, out var info)) continue;
            if (!hasA && groupA.Contains(info.ItemNumber)) hasA = true;
            if (!hasB && groupB.Contains(info.ItemNumber)) hasB = true;
            if (hasA && hasB) break;
        }

        if (!hasA || !hasB)
        {
            item.UnitPrice = 0;
            item.RebatePerUnit = 0;
            return;
        }

        ApplyPercentageOfAssociated(item, config, items, billingItemsById, scheduleOverride,
            info => groupA.Contains(info.ItemNumber) || groupB.Contains(info.ItemNumber) ||
                    groupC.Contains(info.ItemNumber) || groupD.Contains(info.ItemNumber));
    }

    /// <summary>Gated on Group A having a matching sibling (else fee = 0); when open, Fee = CalculateFromFee + sum of sibling fees matching Group A/B/C (raw sum, no percentage, no schedule override).</summary>
    private static void ApplyBasicUnits(
        InvoiceItem item, DerivedItemConfig config, ICollection<InvoiceItem> items,
        IReadOnlyDictionary<int, BillingItemInfo> billingItemsById)
    {
        if (!config.CalculateFromFee.HasValue || config.CalculateFromFee.Value <= 0) return;

        var groupA = ItemNumberRangeMatcher.Parse(config.GroupAAssociatedItems);
        var groupB = ItemNumberRangeMatcher.Parse(config.GroupBAssociatedItems);
        var groupC = ItemNumberRangeMatcher.Parse(config.GroupCAssociatedItems);

        var hasA = items.Any(sibling => sibling != item && sibling.BillingItemId.HasValue &&
            billingItemsById.TryGetValue(sibling.BillingItemId.Value, out var info) && groupA.Contains(info.ItemNumber));
        if (!hasA)
        {
            item.UnitPrice = 0;
            item.RebatePerUnit = 0;
            return;
        }

        var fee = config.CalculateFromFee.Value;
        var rebate = config.CalculateFromFee.Value;
        foreach (var sibling in items)
        {
            if (sibling == item || !sibling.BillingItemId.HasValue) continue;
            if (!billingItemsById.TryGetValue(sibling.BillingItemId.Value, out var info)) continue;
            if (!groupA.Contains(info.ItemNumber) && !groupB.Contains(info.ItemNumber) && !groupC.Contains(info.ItemNumber)) continue;

            fee += sibling.UnitPrice;
            rebate += sibling.RebatePerUnit;
        }

        item.UnitPrice = fee;
        item.RebatePerUnit = rebate;
    }

    /// <summary>
    /// 3-path: (A) CalculateFromFee as base, (B) DerivedItemRateCalculated precalculated override keyed by
    /// patient count, (C) the single associated item's own fee as base. All paths then apply the
    /// patient-count-plus formula and optional gap.
    /// </summary>
    private async Task ApplyNumberOfPatientsSeenAsync(InvoiceItem item, DerivedItemConfig config, int? feeScheduleId, FeeScheduleItem? scheduleOverride)
    {
        if (!config.NumOfLimitPatient.HasValue || config.NumOfLimitPatient.Value <= 0 ||
            !config.UnderNumOfLimitPatientPlusTotal.HasValue ||
            !config.OverNumOfLimitPatientPlus.HasValue)
        {
            item.UnitPrice = 0;
            item.RebatePerUnit = 0;
            return;
        }

        var patientCount = item.DerivedQuantity is > 0 ? item.DerivedQuantity.Value : 1m;

        // Path A: configured flat base fee.
        if (config.CalculateFromFee.HasValue && config.CalculateFromFee.Value > 0)
        {
            item.UnitPrice = ApplyPatientCountAndGap(config, config.CalculateFromFee.Value, patientCount, ResolveGapPercent(scheduleOverride));
            item.RebatePerUnit = ApplyPatientCountAndGap(config, config.CalculateFromFee.Value, patientCount, gapPercent: null);
            return;
        }

        // Path B: precalculated override keyed by patient count.
        if (scheduleOverride != null)
        {
            var orderNum = (int)Math.Round(patientCount > config.NumOfLimitPatient.Value ? config.NumOfLimitPatient.Value + 1 : patientCount);
            var precalculated = scheduleOverride.DerivedItemRateCalculateds.FirstOrDefault(d => d.OrderNum == orderNum);
            if (precalculated != null)
            {
                item.UnitPrice = precalculated.Fee;
                item.RebatePerUnit = precalculated.Fee;
                return;
            }
        }

        // Path C: the (single) associated item's own fee as base.
        if (config.AssociatedBillingItemId.HasValue)
        {
            var (baseFee, baseRebate) = await ResolveAssociatedItemFeeAsync(config.AssociatedBillingItemId.Value, feeScheduleId);
            item.UnitPrice = ApplyPatientCountAndGap(config, baseFee, patientCount, ResolveGapPercent(scheduleOverride));
            item.RebatePerUnit = ApplyPatientCountAndGap(config, baseRebate, patientCount, gapPercent: null);
        }
    }

    private static decimal ApplyPatientCountAndGap(DerivedItemConfig config, decimal baseFee, decimal patientCount, decimal? gapPercent)
    {
        var fee = baseFee;
        fee += patientCount <= config.NumOfLimitPatient!.Value
            ? config.UnderNumOfLimitPatientPlusTotal!.Value / patientCount
            : config.OverNumOfLimitPatientPlus!.Value;

        if (gapPercent.HasValue)
            fee *= gapPercent.Value / 100m;

        return fee;
    }

    /// <summary>Base = associated item's fee. Checks a precalculated override (keyed by quantity) first; else if quantity exceeds the limit, adds (quantity - limit) x OverLimitQuantityPlus.</summary>
    private async Task ApplyFieldQuantityAsync(InvoiceItem item, DerivedItemConfig config, int? feeScheduleId, FeeScheduleItem? scheduleOverride)
    {
        if (!config.NumberOfLimitQuantity.HasValue || config.NumberOfLimitQuantity.Value <= 0 ||
            !config.OverLimitQuantityPlus.HasValue || config.OverLimitQuantityPlus.Value <= 0 ||
            !config.AssociatedBillingItemId.HasValue)
        {
            item.UnitPrice = 0;
            item.RebatePerUnit = 0;
            return;
        }

        var quantity = item.DerivedQuantity is > 0 ? item.DerivedQuantity.Value : 1m;

        if (scheduleOverride != null)
        {
            var precalculated = scheduleOverride.DerivedItemRateCalculateds.FirstOrDefault(d => d.OrderNum == (int)Math.Round(quantity));
            if (precalculated != null)
            {
                item.UnitPrice = precalculated.Fee;
                item.RebatePerUnit = precalculated.Fee;
                return;
            }
        }

        var (baseFee, baseRebate) = await ResolveAssociatedItemFeeAsync(config.AssociatedBillingItemId.Value, feeScheduleId);
        var overLimitPlus = scheduleOverride?.OverLimitQuantityPlus is > 0 ? scheduleOverride.OverLimitQuantityPlus.Value : config.OverLimitQuantityPlus.Value;
        var gapPercent = ResolveGapPercent(scheduleOverride);

        if (quantity > config.NumberOfLimitQuantity.Value)
        {
            var overLimitQuantity = quantity - config.NumberOfLimitQuantity.Value;
            baseFee += overLimitQuantity * overLimitPlus;
            baseRebate += overLimitQuantity * overLimitPlus;
        }

        if (gapPercent.HasValue)
            baseFee *= gapPercent.Value / 100m;

        item.UnitPrice = baseFee;
        item.RebatePerUnit = baseRebate;
    }

    /// <summary>Base = associated item's fee. Always adds quantity x OverLimitQuantityPlus (no threshold subtraction, unlike FieldQuantity).</summary>
    private async Task ApplyTimeDurationAsync(InvoiceItem item, DerivedItemConfig config, int? feeScheduleId, FeeScheduleItem? scheduleOverride)
    {
        if (!config.OverLimitQuantityPlus.HasValue || config.OverLimitQuantityPlus.Value <= 0 ||
            !config.AssociatedBillingItemId.HasValue)
        {
            item.UnitPrice = 0;
            item.RebatePerUnit = 0;
            return;
        }

        var quantity = item.DerivedQuantity ?? 0m;
        var (baseFee, baseRebate) = await ResolveAssociatedItemFeeAsync(config.AssociatedBillingItemId.Value, feeScheduleId);
        var overLimitPlus = scheduleOverride?.OverLimitQuantityPlus is > 0 ? scheduleOverride.OverLimitQuantityPlus.Value : config.OverLimitQuantityPlus.Value;
        var gapPercent = ResolveGapPercent(scheduleOverride);

        baseFee += quantity * overLimitPlus;
        baseRebate += quantity * overLimitPlus;

        if (gapPercent.HasValue)
            baseFee *= gapPercent.Value / 100m;

        item.UnitPrice = baseFee;
        item.RebatePerUnit = baseRebate;
    }

    /// <summary>
    /// Resolves an associated item's fee for formulas that need it independent of whether that item is
    /// actually an invoice line (FieldQuantity/TimeDuration/NumberOfPatientsSeen path C): the fee
    /// schedule's own price for the item, falling back to the MBS ScheduleFee. Rebate uses the same
    /// value for both channels — we don't have a rebate-specific lookup for a hypothetical non-invoice
    /// item, unlike the sibling-line-based formulas above.
    /// </summary>
    private async Task<(decimal Fee, decimal Rebate)> ResolveAssociatedItemFeeAsync(int associatedBillingItemId, int? feeScheduleId)
    {
        if (feeScheduleId.HasValue)
        {
            var scheduleFee = await _context.FeeScheduleItems
                .Where(f => f.FeeScheduleId == feeScheduleId.Value && f.BillingItemId == associatedBillingItemId)
                .Select(f => (decimal?)f.Fee)
                .FirstOrDefaultAsync();
            if (scheduleFee.HasValue)
                return (scheduleFee.Value, scheduleFee.Value);
        }

        var mbsFee = await _context.BillingItems
            .Where(b => b.Id == associatedBillingItemId)
            .Select(b => (decimal?)b.ScheduleFee)
            .FirstOrDefaultAsync();
        return (mbsFee ?? 0m, mbsFee ?? 0m);
    }

    /// <summary>Either/or: a per-schedule PercentageFromAssociatedItemFee override replaces the config default; otherwise a per-schedule MedicalGapPercent (if set) is returned to be applied after the sum.</summary>
    private static decimal ResolveConfigPercentage(decimal? configPercentage, FeeScheduleItem? scheduleOverride, out decimal? gapPercent)
    {
        gapPercent = null;
        if (scheduleOverride?.PercentageFromAssociatedItemFee is > 0)
            return scheduleOverride.PercentageFromAssociatedItemFee.Value;

        gapPercent = ResolveGapPercent(scheduleOverride);
        return configPercentage ?? 0m;
    }

    private static decimal? ResolveGapPercent(FeeScheduleItem? scheduleOverride) =>
        scheduleOverride?.MedicalGapPercent is > 0 ? scheduleOverride.MedicalGapPercent : null;
}

using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Applies derived-item fee formulas as a post-pass over an invoice's line items — run after every
/// line has already been resolved normally by IBillingCalculator (and abated by
/// IMultipleOperationRuleCalculator, if applicable), since the associative calculation types need a
/// sibling line's already-resolved fee as input. Covers a scoped subset of legacy's 10-strategy
/// DerivedFeeCalculator (see BillingEnums.cs' DerivedCalculationType for what's included and the
/// implementation plan for what's explicitly deferred: ProcedureDiscontinued, ExcisionMalignantTumour,
/// CombinationOperations, AdditionalFoetusTested/OriginalAmputationFee, OtherDerivedItemInfo, and
/// DerivedItemRateCalculated overrides).
/// PercentageOfAssociatedItem and AssistanceAnaesthesia compute identically — both are flat-percentage
/// modifiers (e.g. the assistant-at-surgery fee, or an after-hours assistance-at-anaesthesia loading
/// like MBS item 25030's "50% of the fee for assistance at anaesthesia"); real MBS anaesthesia time
/// units are separately-billed items with their own published fee, not a formula, so this deliberately
/// does not attempt to compute anaesthesia basic/time units.
/// </summary>
public class DerivedFeeCalculator : IDerivedFeeCalculator
{
    private readonly AppDbContext _context;

    public DerivedFeeCalculator(AppDbContext context)
    {
        _context = context;
    }

    public async Task ApplyDerivedFeesAsync(ICollection<InvoiceItem> items)
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

        // Needed for AssociatedGroup matching — looked up once for every billing item on the
        // invoice (not just configured ones), since any sibling line might be the match.
        var groupsByBillingItemId = configs.Values.Any(c => c.AssociatedGroup != null)
            ? await _context.BillingItems.Where(b => billingItemIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Group)
            : new Dictionary<int, string?>();

        foreach (var item in items)
        {
            if (!item.BillingItemId.HasValue || !configs.TryGetValue(item.BillingItemId.Value, out var config))
                continue;

            switch (config.CalculationType)
            {
                case DerivedCalculationType.PercentageOfAssociatedItem:
                case DerivedCalculationType.AssistanceAnaesthesia:
                    ApplyAssociativeFee(item, config, items, groupsByBillingItemId);
                    break;

                case DerivedCalculationType.BasicUnits:
                case DerivedCalculationType.FieldQuantity:
                case DerivedCalculationType.TimeDuration:
                case DerivedCalculationType.NumberOfPatientsSeen:
                    ApplyQuantityDrivenFee(item, config);
                    break;
            }
        }
    }

    /// <summary>
    /// Scales an already-resolved (and possibly abated) base fee/rebate by a configured flat percentage.
    /// AssociatedBillingItemId matches one fixed sibling item. AssociatedGroup instead sums EVERY sibling
    /// line in a given MBS Group (e.g. "T8") — this is what the real assistant-at-surgery rule needs:
    /// MBS defines the item 51303 fee as 20% of the surgeon's *total* abated fee across every eligible
    /// operation performed on the same occasion, not just the single highest-fee one. Picking only the
    /// top item would understate the fee whenever more than one operation in the group was billed.
    /// </summary>
    private static void ApplyAssociativeFee(InvoiceItem item, DerivedItemConfig config, ICollection<InvoiceItem> items, IReadOnlyDictionary<int, string?> groupsByBillingItemId)
    {
        decimal baseFee, baseRebate;
        if (config.AssociatedBillingItemId.HasValue)
        {
            var sibling = items.FirstOrDefault(i => i != item && i.BillingItemId == config.AssociatedBillingItemId);
            if (sibling == null) return;
            baseFee = sibling.UnitPrice;
            baseRebate = sibling.RebatePerUnit;
        }
        else if (!string.IsNullOrEmpty(config.AssociatedGroup))
        {
            var siblings = items
                .Where(i => i != item && i.BillingItemId.HasValue
                            && groupsByBillingItemId.TryGetValue(i.BillingItemId.Value, out var group)
                            && group == config.AssociatedGroup)
                .ToList();
            if (siblings.Count == 0) return;
            baseFee = siblings.Sum(i => i.UnitPrice);
            baseRebate = siblings.Sum(i => i.RebatePerUnit);
        }
        else
        {
            return;
        }

        var percentage = (config.Percentage ?? 0m) / 100m;
        item.UnitPrice = percentage * baseFee;
        item.RebatePerUnit = percentage * baseRebate;
    }

    /// <summary>A manually-entered per-line quantity (minutes/fields/patients/units) times a configured unit value, with an optional over-limit tier.</summary>
    private static void ApplyQuantityDrivenFee(InvoiceItem item, DerivedItemConfig config)
    {
        var quantity = item.DerivedQuantity ?? 0m;
        var unitValue = config.UnitValue ?? 0m;

        decimal fee;
        if (config.LimitQuantity.HasValue && quantity > config.LimitQuantity.Value)
        {
            var overLimitQuantity = quantity - config.LimitQuantity.Value;
            fee = config.LimitQuantity.Value * unitValue + overLimitQuantity * (config.OverLimitUnitValue ?? unitValue);
        }
        else
        {
            fee = quantity * unitValue;
        }

        item.UnitPrice = fee;
    }
}

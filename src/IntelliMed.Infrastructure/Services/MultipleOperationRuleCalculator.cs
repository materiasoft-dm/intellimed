using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Implements the MBS "Multiple Operation Rule" (Note TN.8.2): when 2+ Group T8 (Operations) items
/// are billed on the same occasion, the fee is abated to 100% for the highest-fee item, 50% for the
/// next, and 25% for each remaining item — the fee/rebate reduction cascades into any derived-item
/// formula computed off these lines (e.g. the assistant-at-surgery fee, which MBS defines as one-fifth
/// of the *abated* fee, not the raw one), which is why this must run before IDerivedFeeCalculator.
///
/// WorkCover QLD runs a different, split-pool version of the rule (confirmed against WorkSafe QLD's
/// live "Multiple operation rule" page, not guessed): Subgroups 1-11, 13, 16, 17 ("Surgical") still get
/// 100%/50%/25%, but Subgroups 14 and 15 ("Orthopaedic/Hand") get 100% for the highest, then a FLAT 75%
/// for every other item in that pool (not a taper). The two pools rank and abate independently, so a
/// session mixing both can land two separate items at 100%. Subgroup 12 (amputations) is excluded from
/// the rule entirely under both standard Medicare and WorkCover QLD — previously unmodelled here since
/// SubGroup wasn't imported; now that it is, the exclusion applies uniformly to both branches.
/// </summary>
public class MultipleOperationRuleCalculator : IMultipleOperationRuleCalculator
{
    private const string OperationsGroup = "T8";
    private static readonly decimal[] StandardAbatementFactors = { 1.00m, 0.50m, 0.25m };
    private static readonly HashSet<string> AmputationSubGroup = new() { "12" };
    private static readonly HashSet<string> OrthopaedicHandSubGroups = new() { "14", "15" };

    private readonly AppDbContext _context;

    public MultipleOperationRuleCalculator(AppDbContext context)
    {
        _context = context;
    }

    public async Task ApplyMultipleOperationRuleAsync(ICollection<InvoiceItem> items, AccountTypeEnum accountType)
    {
        var billingItemIds = items
            .Where(i => i.BillingItemId.HasValue)
            .Select(i => i.BillingItemId!.Value)
            .Distinct()
            .ToList();
        if (billingItemIds.Count == 0) return;

        var billingItemsById = await _context.BillingItems
            .Where(b => billingItemIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => (b.Group, b.SubGroup));

        var operationItems = items
            .Where(i => i.BillingItemId.HasValue
                        && billingItemsById.TryGetValue(i.BillingItemId.Value, out var info)
                        && info.Group == OperationsGroup
                        && !(info.SubGroup != null && AmputationSubGroup.Contains(info.SubGroup))) // Subgroup 12 (amputations) is never subject to the rule
            .ToList();
        if (operationItems.Count == 0) return;

        if (accountType == AccountTypeEnum.WorkCover)
        {
            ApplyWorkCoverQldSplitPoolRule(operationItems, billingItemsById);
        }
        else
        {
            ApplyAbatement(operationItems, StandardAbatementFactors);
        }
    }

    /// <summary>WorkCover QLD: Subgroups 14/15 ranked and abated separately from every other (non-excluded) subgroup.</summary>
    private void ApplyWorkCoverQldSplitPoolRule(List<InvoiceItem> operationItems, IReadOnlyDictionary<int, (string? Group, string? SubGroup)> billingItemsById)
    {
        var orthopaedicPool = new List<InvoiceItem>();
        var surgicalPool = new List<InvoiceItem>();

        foreach (var item in operationItems)
        {
            var subGroup = billingItemsById[item.BillingItemId!.Value].SubGroup;
            if (subGroup != null && OrthopaedicHandSubGroups.Contains(subGroup))
                orthopaedicPool.Add(item);
            else
                surgicalPool.Add(item); // includes null/unrecognised SubGroup — safer default than silently skipping abatement
        }

        ApplyAbatement(surgicalPool, StandardAbatementFactors);
        ApplyFlatAfterFirstAbatement(orthopaedicPool, 0.75m);
    }

    /// <summary>Standard 3-tier cascade: 100% highest, then each subsequent factor, holding the last factor for any remaining items.</summary>
    private static void ApplyAbatement(List<InvoiceItem> pool, decimal[] factors)
    {
        if (pool.Count < 2) return;

        var ranked = pool.OrderByDescending(i => i.UnitPrice).ToList();
        for (var rank = 0; rank < ranked.Count; rank++)
        {
            var factor = factors[Math.Min(rank, factors.Length - 1)];
            Abate(ranked[rank], factor);
        }
    }

    /// <summary>WorkCover QLD orthopaedic/hand shape: 100% for the highest, then one flat factor for every other item (no taper).</summary>
    private static void ApplyFlatAfterFirstAbatement(List<InvoiceItem> pool, decimal afterFirstFactor)
    {
        if (pool.Count < 2) return;

        var ranked = pool.OrderByDescending(i => i.UnitPrice).ToList();
        for (var rank = 0; rank < ranked.Count; rank++)
        {
            Abate(ranked[rank], rank == 0 ? 1.00m : afterFirstFactor);
        }
    }

    private static void Abate(InvoiceItem item, decimal factor)
    {
        item.UnitPrice = BillingMath.RoundMoney(item.UnitPrice * factor);
        item.RebatePerUnit = BillingMath.RoundMoney(item.RebatePerUnit * factor);
    }
}

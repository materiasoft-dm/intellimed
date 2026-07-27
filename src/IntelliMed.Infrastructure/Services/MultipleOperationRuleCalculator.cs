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
/// Known simplification: the real rule excludes Subgroup 12 (amputations) from abatement — we don't
/// import MBS SubGroup data, so that exclusion isn't modeled here.
/// </summary>
public class MultipleOperationRuleCalculator : IMultipleOperationRuleCalculator
{
    private const string OperationsGroup = "T8";
    private static readonly decimal[] AbatementFactors = { 1.00m, 0.50m, 0.25m };

    private readonly AppDbContext _context;

    public MultipleOperationRuleCalculator(AppDbContext context)
    {
        _context = context;
    }

    public async Task ApplyMultipleOperationRuleAsync(ICollection<InvoiceItem> items)
    {
        var billingItemIds = items
            .Where(i => i.BillingItemId.HasValue)
            .Select(i => i.BillingItemId!.Value)
            .Distinct()
            .ToList();
        if (billingItemIds.Count == 0) return;

        var groupsByBillingItemId = await _context.BillingItems
            .Where(b => billingItemIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Group);

        var operationItems = items
            .Where(i => i.BillingItemId.HasValue
                        && groupsByBillingItemId.TryGetValue(i.BillingItemId.Value, out var group)
                        && group == OperationsGroup)
            .OrderByDescending(i => i.UnitPrice)
            .ToList();
        if (operationItems.Count < 2) return;

        for (var rank = 0; rank < operationItems.Count; rank++)
        {
            var factor = AbatementFactors[Math.Min(rank, AbatementFactors.Length - 1)];
            var item = operationItems[rank];
            item.UnitPrice = BillingMath.RoundMoney(item.UnitPrice * factor);
            item.RebatePerUnit = BillingMath.RoundMoney(item.RebatePerUnit * factor);
        }
    }
}

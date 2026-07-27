using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

/// <summary>
/// Applies the MBS "Multiple Operation Rule" (Note TN.8.2) as a post-pass over an invoice's
/// already-resolved line items — abating the fee/rebate of the second and subsequent Group T8
/// (Operations) items billed on the same occasion, since that's a whole-invoice concern rather
/// than something IBillingCalculator can resolve one line at a time.
/// </summary>
public interface IMultipleOperationRuleCalculator
{
    Task ApplyMultipleOperationRuleAsync(ICollection<InvoiceItem> items);
}

using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

/// <summary>
/// Applies derived-item fee formulas (assistant-at-surgery, patients-seen, time-loading, etc.) as a
/// post-pass over an invoice's already-resolved line items — these formulas need the whole invoice's
/// lines as context, unlike IBillingCalculator's per-line resolution.
/// </summary>
public interface IDerivedFeeCalculator
{
    Task ApplyDerivedFeesAsync(ICollection<InvoiceItem> items);
}

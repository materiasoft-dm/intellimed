namespace IntelliMed.Core.Entities;

/// <summary>
/// One receipting transaction: money (or credit) received from a payer, applied across one or
/// more invoices/line items in a single transaction. Replaces the flat, invoice-only Payment.
/// </summary>
public class Receipt
{
    public int Id { get; set; }
    public int ClinicId { get; set; }

    /// <summary>Who actually paid — resolved as Client.PayerClientId ?? Client.Id at receipt-creation
    /// time, so this is always concretely populated (never ambiguous about self-pay vs. a parent/
    /// guardian payer).</summary>
    public int PayerClientId { get; set; }

    public DateTime ReceiptDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The legacy Pracnet Receipts.GUID this record was imported from (PrimaryClinic
    /// Migrator), or — for receipts backfilled from a pre-rebuild flat Payment row — that Payment's
    /// own LegacyGuid (or a synthesized "legacy-payment:{Id}" key when the source Payment had none).</summary>
    public string? LegacyGuid { get; set; }

    // Navigation properties
    public Client? PayerClient { get; set; }
    public ICollection<ReceiptPayment> Payments { get; set; } = new List<ReceiptPayment>();
    public ICollection<ReceiptAllocation> Allocations { get; set; } = new List<ReceiptAllocation>();
}

/// <summary>One tender within a Receipt (e.g. "$60 Cash", "$40 EFTPOS"). Deliberately not linked to
/// specific ReceiptAllocation rows — a receipt's tenders fund the pool of allocations, not specific
/// cells; see ReceiptAllocation remarks.</summary>
public class ReceiptPayment
{
    public int Id { get; set; }
    public int ReceiptId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? LegacyGuid { get; set; }

    public Receipt? Receipt { get; set; }
}

/// <summary>
/// One settlement within a Receipt — money applied to an invoice/line item, or a payer-level
/// credit. Nullability of InvoiceId/InvoiceItemId carries three distinct meanings, enforced in
/// ReceiptRepository (this codebase has no DB CHECK constraints anywhere):
///   - InvoiceId set, InvoiceItemId null  -> whole-invoice-level allocation (no line precision)
///   - InvoiceId set, InvoiceItemId set   -> line-item-level allocation (full precision)
///   - InvoiceId null, InvoiceItemId null -> payer-level credit (AllocationType must be Credit)
///   - InvoiceId null, InvoiceItemId set  -> never valid.
/// Deliberately not linked to specific ReceiptPayment rows — no cell matrix; the receipt-level
/// invariant sum(Payments) == sum(Allocations) is enough.
/// </summary>
public class ReceiptAllocation
{
    public int Id { get; set; }
    public int ReceiptId { get; set; }
    public int? InvoiceId { get; set; }
    public int? InvoiceItemId { get; set; }
    public decimal Amount { get; set; }
    public AllocationType AllocationType { get; set; } = AllocationType.Payment;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Legacy composite "{ReceiptGUID}:{InvoiceGUID}" (mirrors the old Payment.LegacyGuid's
    /// format, now anchored at the allocation instead of the payment), or the backfill-source
    /// Payment's LegacyGuid.</summary>
    public string? LegacyGuid { get; set; }

    public Receipt? Receipt { get; set; }
    public Invoice? Invoice { get; set; }
    public InvoiceItem? InvoiceItem { get; set; }
}

/// <summary>
/// Append-only — persisted as a plain int, so inserting a value in the middle later would silently
/// reinterpret existing rows; never renumber.
/// </summary>
public enum AllocationType
{
    Payment,
    Credit,
    Refund
}

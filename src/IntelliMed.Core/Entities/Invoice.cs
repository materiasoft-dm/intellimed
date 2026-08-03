using System.ComponentModel.DataAnnotations.Schema;

namespace IntelliMed.Core.Entities;

public class Invoice
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public int ClientId { get; set; }
    public int? AppointmentId { get; set; }
    public int? PractitionerId { get; set; }
    public AccountTypeEnum AccountType { get; set; } = AccountTypeEnum.PrivatePatient;
    public PlaceOfServiceEnum PlaceOfService { get; set; } = PlaceOfServiceEnum.Rooms;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountWrittenOff { get; set; }
    public decimal AmountOwing => TotalAmount - AmountPaid - AmountWrittenOff;
    public string? Notes { get; set; }
    public string? CancelReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The legacy Pracnet Invoices.GUID this record was imported from (PrimaryClinic Migrator).</summary>
    public string? LegacyGuid { get; set; }

    public bool ClaimSubmissionAuthorised { get; set; }
    public bool FinancialInterestDisclosed { get; set; }
    public bool CompensationClaim { get; set; }
    public bool SubmissionAuthorityReceived { get; set; }
    public bool BenefitAssignmentRequested { get; set; }
    public ClaimStatus ClaimStatus { get; set; } = ClaimStatus.NotSubmitted;

    /// <summary>Invoice-level override for who receives the Medicare/DVA/fund payment when different
    /// from the servicing Practitioner. Null = same as Practitioner (legacy:
    /// GroupPayeeBusinessAddressGuid = PayeeBusinessAddressGUID ?? ProviderBusinessAddressGUID).
    /// Deliberately no provider-level default (legacy's ProviderBusinessAddresses.PayeeProviderGUID) —
    /// that's a UX auto-fill convenience worth adding later if manual per-invoice selection turns out
    /// to be a real annoyance, not something to build speculatively now.</summary>
    public int? PayeePractitionerId { get; set; }

    // Navigation properties
    public Client? Client { get; set; }
    public Appointment? Appointment { get; set; }
    public Practitioner? Practitioner { get; set; }
    public Practitioner? PayeePractitioner { get; set; }
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<ReceiptAllocation> ReceiptAllocations { get; set; } = new List<ReceiptAllocation>();
    public ICollection<InvoiceWriteOff> WriteOffs { get; set; } = new List<InvoiceWriteOff>();
}

/// <summary>A partial or full forgiveness of an invoice's outstanding balance — no money moves, so
/// unlike a Refund this never touches Receipt/ReceiptPayment/ReceiptAllocation at all. Invoice-level
/// only (no line-item precision) for a basic first pass.</summary>
public class InvoiceWriteOff
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Invoice? Invoice { get; set; }
}

public class InvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int? BillingItemId { get; set; }

    /// <summary>Per-line fee schedule override — legacy lets each invoice line pick its own schedule (e.g. bulk-bill one service, privately bill another on the same invoice). Null falls back to the invoice-level default (ClinicId + AccountType + HealthFundId).</summary>
    public int? FeeScheduleId { get; set; }

    public string Description { get; set; } = string.Empty;
    public DateTime? ServiceDate { get; set; }
    public int Quantity { get; set; } = 1;

    // UnitPrice is the charged fee per unit (legacy: InvItem.Fee).
    public decimal UnitPrice { get; set; }

    // The bulk-bill-equivalent Medicare rebate per unit (legacy: RebatePerItem).
    public decimal RebatePerUnit { get; set; }

    // Line-level discount and GST (AU medical is GST-exempt, so GST defaults to 0).
    public decimal Discount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal PercentGst { get; set; }
    public bool FeeIncludeGst { get; set; }

    // Manually-entered per-line quantity feeding a derived-item formula (minutes/fields/patients/units) —
    // distinct from Quantity, which is a plain repeat-count multiplier.
    public decimal? DerivedQuantity { get; set; }

    // Computed money fields, matching legacy InvItem semantics.
    [NotMapped]
    public decimal LineRebate => RebatePerUnit * Quantity;
    [NotMapped]
    public decimal NetAmount => UnitPrice * Quantity + GstAmount;
    [NotMapped]
    public decimal TotalPrice => NetAmount - Discount;
    [NotMapped]
    public decimal Gap => TotalPrice - LineRebate;

    /// <summary>The legacy Pracnet InvoiceItems.GUID this record was imported from (PrimaryClinic Migrator).</summary>
    public string? LegacyGuid { get; set; }

    /// <summary>Free-text note attached to this line item — shown via the "i" icon in the line-items table.</summary>
    public string? Note { get; set; }

    // Navigation properties
    public Invoice? Invoice { get; set; }
    public BillingItem? BillingItem { get; set; }
    public FeeSchedule? FeeSchedule { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Idempotency key for PrimaryClinic Migrator re-imports. Not a raw legacy GUID — legacy Receipts
    /// can pay off multiple invoices at once, so this is an opaque "{ReceiptGUID}:{InvoiceLegacyGuid}"
    /// composite, unique per (receipt, invoice) pair produced by the aggregation.
    /// </summary>
    public string? LegacyGuid { get; set; }

    // Navigation property
    public Invoice? Invoice { get; set; }
}

public enum InvoiceStatus
{
    Draft,
    Sent,
    Paid,
    PartiallyPaid,
    Overdue,
    Cancelled
}

/// <summary>Medicare/DVA/health-fund claim lodgement status — orthogonal to InvoiceStatus (payment
/// state), and unrelated to ClientCompensationClaim (a WorkCover/TAC injury-claim record — "claim"
/// is an overloaded word in this domain). Append-only, like AllocationType — persisted as a plain
/// int, never renumber. Deliberately collapsed from legacy's ~20-value eClaimStatus (which encodes
/// per-channel/vendor history no code here needs yet) down to the essential lifecycle; nothing
/// drives real transitions until real electronic claiming exists, so this is manually set for now.</summary>
public enum ClaimStatus
{
    NotSubmitted,
    Submitted,
    Accepted,
    Rejected,
    PartiallyPaid,
    Paid
}

public enum PaymentMethod
{
    Cash,
    Cheque,
    Eftpos,
    CreditCard,
    BankTransfer,
    Medicare,
    Dva,
    Other
}
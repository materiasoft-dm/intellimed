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
    public decimal AmountOwing => TotalAmount - AmountPaid;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The legacy Pracnet Invoices.GUID this record was imported from (PrimaryClinic Migrator).</summary>
    public string? LegacyGuid { get; set; }

    // Navigation properties
    public Client? Client { get; set; }
    public Appointment? Appointment { get; set; }
    public Practitioner? Practitioner { get; set; }
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
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
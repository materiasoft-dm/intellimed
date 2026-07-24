namespace IntelliMed.Core.Entities;

public class Invoice
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public int ClientId { get; set; }
    public int? AppointmentId { get; set; }
    public int? PractitionerId { get; set; }
    public AccountTypeEnum AccountType { get; set; } = AccountTypeEnum.PrivatePatient;
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
    public string Description { get; set; } = string.Empty;
    public DateTime? ServiceDate { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;

    // Navigation properties
    public Invoice? Invoice { get; set; }
    public BillingItem? BillingItem { get; set; }
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
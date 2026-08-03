using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class ReceiptDto
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public string? ClinicAbn { get; set; }
    public string? ClinicAddress { get; set; }
    public string? ClinicSuburb { get; set; }
    public string? ClinicState { get; set; }
    public string? ClinicPostcode { get; set; }
    public string? ClinicPhone { get; set; }
    public string? ClinicEmail { get; set; }
    public int PayerClientId { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ReceiptPaymentDto> Payments { get; set; } = new();
    public List<ReceiptAllocationDto> Allocations { get; set; } = new();
    public decimal TotalTendered => Payments.Sum(p => p.Amount);
}

public class ReceiptPaymentDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string MethodName => Method.ToString();
    public string? Reference { get; set; }
}

public class ReceiptAllocationDto
{
    public int Id { get; set; }
    public int? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public int? InvoiceItemId { get; set; }
    public string? InvoiceItemDescription { get; set; }
    public decimal Amount { get; set; }
    public AllocationType AllocationType { get; set; }
    public string AllocationTypeName => AllocationType.ToString();
}

public class CreateReceiptDto
{
    public int ClinicId { get; set; }
    public int PayerClientId { get; set; }
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public List<CreateReceiptTenderDto> Payments { get; set; } = new();
    public List<CreateReceiptAllocationDto> Allocations { get; set; } = new();

    /// <summary>If tenders exceed allocations: true keeps the excess as payer credit; false throws.</summary>
    public bool KeepOverpaymentAsCredit { get; set; } = true;
}

public class CreateReceiptTenderDto
{
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
}

public class CreateReceiptAllocationDto
{
    public int InvoiceId { get; set; }
    public int? InvoiceItemId { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>Refund either a specific invoice's paid amount (InvoiceId set, capped at that invoice's
/// AmountPaid minus any prior refunds) or a payer's unspent account credit (InvoiceId null, capped
/// at their available Credit balance).</summary>
public class CreateRefundDto
{
    public int ClinicId { get; set; }
    public int PayerClientId { get; set; }
    public int? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public DateTime RefundDate { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>A payer's outstanding invoice, for the multi-invoice receipt picker.</summary>
public class OutstandingInvoiceDto
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountOwing { get; set; }
}

using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class InvoiceDto
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
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public int? PayerClientId { get; set; }
    public string? PayerName { get; set; }
    public int? AppointmentId { get; set; }
    public int? PractitionerId { get; set; }
    public string? PractitionerName { get; set; }
    public AccountTypeEnum AccountType { get; set; }
    public string AccountTypeName => AccountType.ToString();
    public PlaceOfServiceEnum PlaceOfService { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public InvoiceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountWrittenOff { get; set; }
    public decimal AmountOwing => TotalAmount - AmountPaid - AmountWrittenOff;
    public decimal TotalRebate => Items.Sum(i => i.LineRebate);
    public decimal TotalGap => Items.Sum(i => i.Gap);
    public string? Notes { get; set; }
    public string? CancelReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<InvoiceItemDto> Items { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
    public List<PaymentDto> Refunds { get; set; } = new();
    public List<InvoiceWriteOffDto> WriteOffs { get; set; } = new();
}

public class InvoiceItemDto
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int? BillingItemId { get; set; }
    public int? FeeScheduleId { get; set; }
    public string? FeeScheduleCode { get; set; }
    public string? ItemNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? ServiceDate { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal RebatePerUnit { get; set; }
    public decimal Discount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal PercentGst { get; set; }
    public decimal? DerivedQuantity { get; set; }
    public string? Note { get; set; }
    public decimal LineRebate => RebatePerUnit * Quantity;
    public decimal NetAmount => UnitPrice * Quantity + GstAmount;
    public decimal TotalPrice => NetAmount - Discount;
    public decimal Gap => TotalPrice - LineRebate;
}

public class PaymentDto
{
    public int Id { get; set; }
    public int ReceiptId { get; set; }
    public int InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? ClientName { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string MethodName => Method.ToString();
    public string? Reference { get; set; }
    public DateTime PaymentDate { get; set; }
}

public class CreateInvoiceDto
{
    public int ClinicId { get; set; }
    public int ClientId { get; set; }
    public int? AppointmentId { get; set; }
    public int? PractitionerId { get; set; }
    public AccountTypeEnum AccountType { get; set; } = AccountTypeEnum.PrivatePatient;
    public PlaceOfServiceEnum PlaceOfService { get; set; } = PlaceOfServiceEnum.Rooms;
    public DateTime DueDate { get; set; }
    public string? Notes { get; set; }
    public List<CreateInvoiceItemDto> Items { get; set; } = new();
}

public class CreateInvoiceItemDto
{
    public int? BillingItemId { get; set; }

    /// <summary>Per-line fee schedule override — null falls back to the invoice-level default.</summary>
    public int? FeeScheduleId { get; set; }

    public string Description { get; set; } = string.Empty;
    public DateTime? ServiceDate { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal? DerivedQuantity { get; set; }
    public string? Note { get; set; }
}

/// <summary>Request to resolve a line item's fee/rebate/GST for the given billing context (live UI preview).</summary>
public class ResolveLineRequest
{
    public int ClinicId { get; set; }
    public AccountTypeEnum AccountType { get; set; }
    public int? PractitionerId { get; set; }
    public PlaceOfServiceEnum PlaceOfService { get; set; }
    public int BillingItemId { get; set; }
    public DateTime? ServiceDate { get; set; }
    public int? HealthFundId { get; set; }

    /// <summary>Per-line fee schedule override for the live preview — null falls back to the invoice-level default.</summary>
    public int? FeeScheduleId { get; set; }
}

public class ResolveLineResult
{
    public decimal Fee { get; set; }
    public decimal RebatePerUnit { get; set; }
    public decimal GstAmount { get; set; }
    public decimal PercentGst { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class AccountTypeFeeScheduleMappingDto
{
    public AccountTypeEnum AccountType { get; set; }
    public string AccountTypeName => AccountType.ToString();
    public int? FeeScheduleId { get; set; }
    public string? FeeScheduleCode { get; set; }
}

public class SaveAccountTypeFeeScheduleMappingDto
{
    public AccountTypeEnum AccountType { get; set; }
    public int? FeeScheduleId { get; set; }
}

public class CreatePaymentDto
{
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public DateTime PaymentDate { get; set; }
}

public class InvoiceWriteOffDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateWriteOffDto
{
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CancelInvoiceDto
{
    public string Reason { get; set; } = string.Empty;
}

public class SplitInvoiceDto
{
    public string Reason { get; set; } = string.Empty;
    public List<SplitInvoiceGroupDto> NewInvoices { get; set; } = new();
}

public class SplitInvoiceGroupDto
{
    public List<int> InvoiceItemIds { get; set; } = new();
    public DateTime? DueDate { get; set; }
}

public class SplitInvoiceResultDto
{
    public int SourceInvoiceId { get; set; }
    public List<int> NewInvoiceIds { get; set; } = new();
}

public class InvoiceSearchDto
{
    public int? ClinicId { get; set; }
    public int? ClientId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public InvoiceStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class PaymentSearchDto
{
    public int? ClinicId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public PaymentMethod? Method { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

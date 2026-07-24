using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class InvoiceDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public int? AppointmentId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public InvoiceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountOwing => TotalAmount - AmountPaid;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<InvoiceItemDto> Items { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
}

public class InvoiceItemDto
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;
}

public class PaymentDto
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string MethodName => Method.ToString();
    public string? Reference { get; set; }
    public DateTime PaymentDate { get; set; }
}

public class CreateInvoiceDto
{
    public int ClientId { get; set; }
    public int? AppointmentId { get; set; }
    public DateTime DueDate { get; set; }
    public string? Notes { get; set; }
    public List<CreateInvoiceItemDto> Items { get; set; } = new();
}

public class CreateInvoiceItemDto
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public class CreatePaymentDto
{
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public DateTime PaymentDate { get; set; }
}

public class InvoiceSearchDto
{
    public int? ClientId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public InvoiceStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
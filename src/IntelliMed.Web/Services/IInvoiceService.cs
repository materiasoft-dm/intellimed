using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Web.Services;

public interface IInvoiceService
{
    Task<PagedResult<InvoiceDto>> SearchInvoicesAsync(InvoiceSearchDto search);
    Task<InvoiceDto?> GetInvoiceByIdAsync(int id);
    Task<int?> CreateInvoiceAsync(CreateInvoiceDto dto);
    Task<bool> AddPaymentAsync(int invoiceId, CreatePaymentDto dto);
    Task<bool> UpdateInvoiceStatusAsync(int id, InvoiceStatus status);
    Task<PagedResult<PaymentDto>> GetAllPaymentsAsync(PaymentSearchDto search);
}

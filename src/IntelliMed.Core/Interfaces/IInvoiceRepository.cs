using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<InvoiceDto?> GetByIdAsync(int id);
    Task<InvoiceDto?> GetByIdWithDetailsAsync(int id);
    Task<IEnumerable<InvoiceDto>> SearchAsync(InvoiceSearchDto search);
    Task<(IEnumerable<InvoiceDto> Items, int TotalCount)> GetPagedAsync(InvoiceSearchDto search);
    Task<IEnumerable<InvoiceDto>> GetByClientIdAsync(int clientId);
    Task<IEnumerable<InvoiceDto>> GetOverdueInvoicesAsync();
    Task<string> GenerateInvoiceNumberAsync();
    Task<int> CreateAsync(CreateInvoiceDto dto);
    Task<ResolveLineResult> ResolveLineAsync(ResolveLineRequest request);
    Task AddPaymentAsync(int invoiceId, CreatePaymentDto dto);
    Task UpdateStatusAsync(int id, InvoiceStatus status);
    Task<(IEnumerable<PaymentDto> Items, int TotalCount)> GetAllPaymentsAsync(PaymentSearchDto search);
    Task WriteOffAsync(int invoiceId, CreateWriteOffDto dto);
    Task CancelAsync(int invoiceId, CancelInvoiceDto dto);
    Task<SplitInvoiceResultDto> SplitAsync(int sourceInvoiceId, SplitInvoiceDto dto);
}
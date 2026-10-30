using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IInvoiceRepository : IRepository<Invoice>
{
    new Task<InvoiceDto?> GetByIdAsync(int id);
    Task<InvoiceDto?> GetByIdWithDetailsAsync(int id);
    Task<IEnumerable<InvoiceDto>> SearchAsync(InvoiceSearchDto search);
    Task<(IEnumerable<InvoiceDto> Items, int TotalCount)> GetPagedAsync(InvoiceSearchDto search);
    Task<IEnumerable<InvoiceDto>> GetByClientIdAsync(int clientId);
    Task<IEnumerable<InvoiceDto>> GetOverdueInvoicesAsync();
    Task<string> GenerateInvoiceNumberAsync();
    Task<int> CreateAsync(CreateInvoiceDto dto);
    Task<ResolveLineResult> ResolveLineAsync(ResolveLineRequest request);
    Task AddPaymentAsync(int invoiceId, CreatePaymentDto dto);
    /// <summary>Records where the claim has got to. Not exposed over the API on purpose — it is for the
    /// electronic claiming pipeline to call when a claim actually changes state, never for staff to type in.</summary>
    Task UpdateClaimStatusAsync(int id, ClaimStatus claimStatus);
    Task<(IEnumerable<PaymentDto> Items, int TotalCount)> GetAllPaymentsAsync(PaymentSearchDto search);
    Task WriteOffAsync(int invoiceId, CreateWriteOffDto dto);
    Task CancelAsync(int invoiceId, CancelInvoiceDto dto);
    Task<SplitInvoiceResultDto> SplitAsync(int sourceInvoiceId, SplitInvoiceDto dto);
}
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
    Task<bool> UpdateClaimStatusAsync(int id, ClaimStatus claimStatus);
    Task<PagedResult<PaymentDto>> GetAllPaymentsAsync(PaymentSearchDto search);
    Task<ResolveLineResult?> ResolveLineAsync(ResolveLineRequest request);
    Task<List<AccountTypeFeeScheduleMappingDto>> GetFeeScheduleMappingsAsync();
    Task<bool> SaveFeeScheduleMappingAsync(SaveAccountTypeFeeScheduleMappingDto dto);
    Task<ReceiptDto?> GetReceiptByIdAsync(int id);
    Task<int?> CreateReceiptAsync(CreateReceiptDto dto);
    Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesForPayerAsync(int payerClientId, int? excludeInvoiceId);
    Task<bool> WriteOffAsync(int invoiceId, CreateWriteOffDto dto);
    Task<bool> CancelInvoiceAsync(int invoiceId, CancelInvoiceDto dto);
    Task<SplitInvoiceResultDto?> SplitInvoiceAsync(int invoiceId, SplitInvoiceDto dto);
    Task<int?> CreateRefundAsync(CreateRefundDto dto);
    Task<decimal> GetAvailableCreditAsync(int payerClientId);
}

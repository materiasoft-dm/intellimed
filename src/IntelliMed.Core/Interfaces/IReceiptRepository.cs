using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IReceiptRepository : IRepository<Receipt>
{
    Task<ReceiptDto?> GetByIdAsync(int id);
    Task<int> CreateAsync(CreateReceiptDto dto);
    Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesForPayerAsync(int clinicId, int payerClientId, int? excludeInvoiceId = null);
    Task<int> CreateRefundAsync(CreateRefundDto dto);
    Task<decimal> GetAvailableCreditAsync(int clinicId, int payerClientId);
}

using System.Net.Http.Json;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Web.Services;

public class InvoiceService : IInvoiceService
{
    private readonly HttpClient _httpClient;

    public InvoiceService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<InvoiceDto>> SearchInvoicesAsync(InvoiceSearchDto search)
    {
        var args = new List<string>
        {
            $"page={search.Page}",
            $"pageSize={search.PageSize}"
        };

        if (search.ClientId.HasValue) args.Add($"clientId={search.ClientId.Value}");
        if (search.Status.HasValue) args.Add($"status={search.Status.Value}");
        if (search.FromDate.HasValue) args.Add($"fromDate={search.FromDate.Value:yyyy-MM-dd}");
        if (search.ToDate.HasValue) args.Add($"toDate={search.ToDate.Value:yyyy-MM-dd}");

        var uri = "api/invoices/search?" + string.Join("&", args);
        return await _httpClient.GetFromJsonAsync<PagedResult<InvoiceDto>>(uri)
            ?? new PagedResult<InvoiceDto>();
    }

    public async Task<InvoiceDto?> GetInvoiceByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/invoices/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<InvoiceDto>();
    }

    private record CreateResult(int Id);

    public async Task<int?> CreateInvoiceAsync(CreateInvoiceDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/invoices", dto);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<CreateResult>();
            return result?.Id;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create invoice error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> AddPaymentAsync(int invoiceId, CreatePaymentDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/invoices/{invoiceId}/payments", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Add payment error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateInvoiceStatusAsync(int id, InvoiceStatus status)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/invoices/{id}/status", new { status });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update invoice status error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateClaimStatusAsync(int id, ClaimStatus claimStatus)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/invoices/{id}/claim-status", new { claimStatus });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update claim status error: {ex.Message}");
            return false;
        }
    }

    public async Task<ResolveLineResult?> ResolveLineAsync(ResolveLineRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/invoices/resolve-line", request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ResolveLineResult>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Resolve line error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<AccountTypeFeeScheduleMappingDto>> GetFeeScheduleMappingsAsync()
    {
        var results = await _httpClient.GetFromJsonAsync<List<AccountTypeFeeScheduleMappingDto>>("api/invoices/fee-schedule-mappings");
        return results ?? new List<AccountTypeFeeScheduleMappingDto>();
    }

    public async Task<bool> SaveFeeScheduleMappingAsync(SaveAccountTypeFeeScheduleMappingDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("api/invoices/fee-schedule-mappings", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Save fee schedule mapping error: {ex.Message}");
            return false;
        }
    }

    public async Task<PagedResult<PaymentDto>> GetAllPaymentsAsync(PaymentSearchDto search)
    {
        var args = new List<string>
        {
            $"page={search.Page}",
            $"pageSize={search.PageSize}"
        };

        if (search.Method.HasValue) args.Add($"method={search.Method.Value}");
        if (search.FromDate.HasValue) args.Add($"fromDate={search.FromDate.Value:yyyy-MM-dd}");
        if (search.ToDate.HasValue) args.Add($"toDate={search.ToDate.Value:yyyy-MM-dd}");

        var uri = "api/invoices/payments?" + string.Join("&", args);
        return await _httpClient.GetFromJsonAsync<PagedResult<PaymentDto>>(uri)
            ?? new PagedResult<PaymentDto>();
    }

    public async Task<ReceiptDto?> GetReceiptByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/receipts/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ReceiptDto>();
    }

    public async Task<int?> CreateReceiptAsync(CreateReceiptDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/receipts", dto);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<CreateResult>();
            return result?.Id;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create receipt error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesForPayerAsync(int payerClientId, int? excludeInvoiceId)
    {
        var uri = $"api/receipts/outstanding?payerClientId={payerClientId}";
        if (excludeInvoiceId.HasValue) uri += $"&excludeInvoiceId={excludeInvoiceId.Value}";

        var results = await _httpClient.GetFromJsonAsync<List<OutstandingInvoiceDto>>(uri);
        return results ?? new List<OutstandingInvoiceDto>();
    }

    public async Task<bool> WriteOffAsync(int invoiceId, CreateWriteOffDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/invoices/{invoiceId}/write-off", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Write off invoice error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CancelInvoiceAsync(int invoiceId, CancelInvoiceDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/invoices/{invoiceId}/cancel", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Cancel invoice error: {ex.Message}");
            return false;
        }
    }

    public async Task<SplitInvoiceResultDto?> SplitInvoiceAsync(int invoiceId, SplitInvoiceDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/invoices/{invoiceId}/split", dto);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SplitInvoiceResultDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Split invoice error: {ex.Message}");
            return null;
        }
    }

    public async Task<int?> CreateRefundAsync(CreateRefundDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/receipts/refund", dto);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<CreateResult>();
            return result?.Id;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create refund error: {ex.Message}");
            return null;
        }
    }

    public async Task<decimal> GetAvailableCreditAsync(int payerClientId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<decimal>($"api/receipts/available-credit?payerClientId={payerClientId}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Get available credit error: {ex.Message}");
            return 0;
        }
    }
}

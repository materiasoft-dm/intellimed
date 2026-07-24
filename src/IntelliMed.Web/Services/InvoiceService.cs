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
}

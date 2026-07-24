using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class BillingItemService : IBillingItemService
{
    private readonly HttpClient _httpClient;

    public BillingItemService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<BillingItemDto>> SearchAsync(string query)
    {
        var uri = $"api/billing-items/search?query={Uri.EscapeDataString(query)}";
        var results = await _httpClient.GetFromJsonAsync<List<BillingItemDto>>(uri);
        return results ?? new List<BillingItemDto>();
    }

    public async Task<BillingItemSyncResultDto?> SyncAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/billing-items/sync", null);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<BillingItemSyncResultDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Sync billing items error: {ex.Message}");
            return null;
        }
    }
}

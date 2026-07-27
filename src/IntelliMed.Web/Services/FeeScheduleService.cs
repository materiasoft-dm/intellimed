using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class FeeScheduleService : IFeeScheduleService
{
    private readonly HttpClient _httpClient;

    public FeeScheduleService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<FeeScheduleDto>> SearchFeeSchedulesAsync(FeeScheduleSearchDto search)
    {
        var args = new List<string>
        {
            $"page={search.Page}",
            $"pageSize={search.PageSize}",
            $"includeArchived={search.IncludeArchived}"
        };

        if (!string.IsNullOrWhiteSpace(search.Code)) args.Add($"code={Uri.EscapeDataString(search.Code)}");
        if (!string.IsNullOrWhiteSpace(search.Description)) args.Add($"description={Uri.EscapeDataString(search.Description)}");

        var uri = "api/fee-schedules/search?" + string.Join("&", args);
        return await _httpClient.GetFromJsonAsync<PagedResult<FeeScheduleDto>>(uri)
            ?? new PagedResult<FeeScheduleDto>();
    }

    public async Task<List<FeeScheduleDto>> GetAllActiveAsync()
    {
        var results = await _httpClient.GetFromJsonAsync<List<FeeScheduleDto>>("api/fee-schedules/active");
        return results ?? new List<FeeScheduleDto>();
    }

    public async Task<FeeScheduleDto?> GetFeeScheduleByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/fee-schedules/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<FeeScheduleDto>();
    }

    private record CreateResult(int Id);

    public async Task<int?> CreateFeeScheduleAsync(CreateFeeScheduleDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/fee-schedules", dto);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<CreateResult>();
            return result?.Id;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create fee schedule error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateFeeScheduleAsync(int id, UpdateFeeScheduleDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/fee-schedules/{id}", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update fee schedule error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ArchiveFeeScheduleAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/fee-schedules/{id}/archive", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Archive fee schedule error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteFeeScheduleAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/fee-schedules/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Delete fee schedule error: {ex.Message}");
            return false;
        }
    }

    public async Task<List<FeeScheduleItemDto>> GetItemsAsync(int feeScheduleId)
    {
        var results = await _httpClient.GetFromJsonAsync<List<FeeScheduleItemDto>>($"api/fee-schedules/{feeScheduleId}/items");
        return results ?? new List<FeeScheduleItemDto>();
    }

    public async Task<bool> AddItemAsync(int feeScheduleId, CreateFeeScheduleItemDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/fee-schedules/{feeScheduleId}/items", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Add fee schedule item error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateItemFeeAsync(int itemId, decimal fee)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/fee-schedules/items/{itemId}", new UpdateFeeScheduleItemFeeDto { Fee = fee });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update fee schedule item error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RemoveItemAsync(int itemId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/fee-schedules/items/{itemId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Remove fee schedule item error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateItemOverridesAsync(int itemId, UpdateFeeScheduleItemOverridesDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/fee-schedules/items/{itemId}/overrides", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update fee schedule item overrides error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AddPrecalculatedRateAsync(int itemId, SaveDerivedItemRateCalculatedDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/fee-schedules/items/{itemId}/rates", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Add precalculated rate error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RemovePrecalculatedRateAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/fee-schedules/rates/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Remove precalculated rate error: {ex.Message}");
            return false;
        }
    }

    public async Task<int?> CopyFeesFromMbsAsync(int feeScheduleId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/fee-schedules/{feeScheduleId}/items/copy-from-mbs", null);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<int>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Copy fees from MBS error: {ex.Message}");
            return null;
        }
    }

    public async Task<int?> ImportItemsAsync(int feeScheduleId, ImportFeeScheduleItemsRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/fee-schedules/{feeScheduleId}/items/import", request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<int>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Import fee schedule items error: {ex.Message}");
            return null;
        }
    }

    public async Task<SeedBulkBillResultDto?> SeedBulkBillSchedulesAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/fee-schedules/seed-bulk-bill", null);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SeedBulkBillResultDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Seed bulk-bill schedules error: {ex.Message}");
            return null;
        }
    }

    public async Task<SeedBulkBillResultDto?> SeedDvaScheduleShellsAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/fee-schedules/seed-dva", null);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SeedBulkBillResultDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Seed DVA schedule shells error: {ex.Message}");
            return null;
        }
    }

    public async Task<SeedBulkBillResultDto?> SeedWorkCoverQldScheduleShellAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/fee-schedules/seed-workcover-qld", null);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SeedBulkBillResultDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Seed WorkCover QLD schedule shell error: {ex.Message}");
            return null;
        }
    }

    public async Task<FeeScheduleFetchResultDto?> FetchNowAsync(int feeScheduleId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/fee-schedules/{feeScheduleId}/fetch-now", null);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<FeeScheduleFetchResultDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fetch fee schedule now error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<FeeScheduleItemHistoryDto>> GetItemHistoryAsync(int itemId)
    {
        var results = await _httpClient.GetFromJsonAsync<List<FeeScheduleItemHistoryDto>>($"api/fee-schedules/items/{itemId}/history");
        return results ?? new List<FeeScheduleItemHistoryDto>();
    }
}

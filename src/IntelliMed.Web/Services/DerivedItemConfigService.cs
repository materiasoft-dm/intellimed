using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class DerivedItemConfigService : IDerivedItemConfigService
{
    private readonly HttpClient _httpClient;

    public DerivedItemConfigService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<DerivedItemConfigDto>> GetAllAsync()
    {
        var results = await _httpClient.GetFromJsonAsync<List<DerivedItemConfigDto>>("api/derived-item-configs");
        return results ?? new List<DerivedItemConfigDto>();
    }

    public async Task<DerivedItemConfigDto?> GetByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/derived-item-configs/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<DerivedItemConfigDto>();
    }

    private record CreateResult(int Id);

    public async Task<int?> CreateAsync(CreateDerivedItemConfigDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/derived-item-configs", dto);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<CreateResult>();
            return result?.Id;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create derived item config error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateAsync(int id, UpdateDerivedItemConfigDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/derived-item-configs/{id}", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update derived item config error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/derived-item-configs/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Delete derived item config error: {ex.Message}");
            return false;
        }
    }
}

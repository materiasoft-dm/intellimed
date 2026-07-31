using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class SearchActionService : ISearchActionService
{
    private readonly HttpClient _httpClient;

    public SearchActionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<SearchActionDto>?> GetAllActiveAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<SearchActionDto>>("api/search-actions");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetAll search actions error: {ex.Message}");
            return null;
        }
    }

    public async Task<SearchActionDto?> GetByIdAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/search-actions/{id}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SearchActionDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetById search action error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CreateAsync(SaveSearchActionRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/search-actions", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create search action error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateAsync(int id, SaveSearchActionRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/search-actions/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update search action error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ArchiveAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/search-actions/{id}/archive", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Archive search action error: {ex.Message}");
            return false;
        }
    }
}

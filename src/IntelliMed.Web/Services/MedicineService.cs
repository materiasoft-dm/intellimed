using System.Net.Http.Json;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Web.Services;

public class MedicineService : IMedicineService
{
    private readonly HttpClient _httpClient;

    public MedicineService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<MedicineDto>> SearchAsync(string query, MedicineSource? source = null)
    {
        var uri = $"api/medicines/search?query={Uri.EscapeDataString(query)}";
        if (source.HasValue) uri += $"&source={source.Value}";
        var results = await _httpClient.GetFromJsonAsync<List<MedicineDto>>(uri);
        return results ?? new List<MedicineDto>();
    }

    public async Task<MedicineImportResultDto?> ImportAsync(Stream fileStream, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync("api/medicines/import", content);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<MedicineImportResultDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Import medicines error: {ex.Message}");
            return null;
        }
    }

    public async Task<IngredientEnrichmentResultDto?> EnrichIngredientsAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/medicines/ingredients/enrich", null);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<IngredientEnrichmentResultDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Enrich ingredients error: {ex.Message}");
            return null;
        }
    }

    public async Task<MedicineDto?> CreateAsync(CreateMedicineDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/medicines", dto);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<MedicineDto>();
    }

    public async Task<bool> UpdateAsync(int id, UpdateMedicineDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/medicines/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeactivateAsync(int id)
    {
        var response = await _httpClient.PostAsync($"api/medicines/{id}/deactivate", null);
        return response.IsSuccessStatusCode;
    }
}

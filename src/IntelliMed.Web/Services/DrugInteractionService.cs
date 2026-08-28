using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class DrugInteractionService : IDrugInteractionService
{
    private readonly HttpClient _httpClient;

    public DrugInteractionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<DrugInteractionDto>> GetAllAsync()
    {
        var results = await _httpClient.GetFromJsonAsync<List<DrugInteractionDto>>("api/drug-interactions");
        return results ?? new List<DrugInteractionDto>();
    }

    public async Task<bool> CreateAsync(CreateDrugInteractionDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/drug-interactions", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int id, UpdateDrugInteractionDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/drug-interactions/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<DrugInteractionImportResultDto?> ImportAsync(Stream fileStream, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync("api/drug-interactions/import", content);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<DrugInteractionImportResultDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Import drug interactions error: {ex.Message}");
            return null;
        }
    }

    public async Task<byte[]?> ExportCsvAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/drug-interactions/export");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Export drug interactions error: {ex.Message}");
            return null;
        }
    }
}

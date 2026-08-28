using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ProgressNoteService : IProgressNoteService
{
    private readonly HttpClient _httpClient;

    public ProgressNoteService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ProgressNoteDto>> GetByClientIdAsync(int clientId)
    {
        var results = await _httpClient.GetFromJsonAsync<List<ProgressNoteDto>>($"api/clients/{clientId}/progress-notes");
        return results ?? new List<ProgressNoteDto>();
    }

    public async Task<ProgressNoteDto?> GetByIdAsync(int clientId, int id)
    {
        var response = await _httpClient.GetAsync($"api/clients/{clientId}/progress-notes/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ProgressNoteDto>();
    }

    public async Task<int?> CreateAsync(CreateProgressNoteDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{dto.ClientId}/progress-notes", dto);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        return json!.RootElement.GetProperty("id").GetInt32();
    }

    public async Task<bool> UpdateAsync(int clientId, int id, UpdateProgressNoteDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{clientId}/progress-notes/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> FinalizeAsync(int clientId, int id, FinalizeProgressNoteDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{clientId}/progress-notes/{id}/finalize", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddAddendumAsync(int clientId, int id, AddProgressNoteAddendumDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{clientId}/progress-notes/{id}/addenda", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int clientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/clients/{clientId}/progress-notes/{id}");
        return response.IsSuccessStatusCode;
    }
}

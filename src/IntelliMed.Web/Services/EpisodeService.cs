using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class EpisodeService : IEpisodeService
{
    private readonly HttpClient _httpClient;

    public EpisodeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<EpisodeDto>> GetByClientIdAsync(int clientId, bool includeArchived = false)
    {
        var uri = $"api/clients/{clientId}/episodes?includeArchived={includeArchived}";
        var results = await _httpClient.GetFromJsonAsync<List<EpisodeDto>>(uri);
        return results ?? new List<EpisodeDto>();
    }

    public async Task<int?> CreateAsync(CreateEpisodeDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{dto.ClientId}/episodes", dto);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        return json!.RootElement.GetProperty("id").GetInt32();
    }

    public async Task<bool> UpdateAsync(int clientId, int id, UpdateEpisodeDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{clientId}/episodes/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResolveAsync(int clientId, int id)
    {
        var response = await _httpClient.PostAsync($"api/clients/{clientId}/episodes/{id}/resolve", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ReopenAsync(int clientId, int id)
    {
        var response = await _httpClient.PostAsync($"api/clients/{clientId}/episodes/{id}/reopen", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ArchiveAsync(int clientId, int id)
    {
        var response = await _httpClient.PostAsync($"api/clients/{clientId}/episodes/{id}/archive", null);
        return response.IsSuccessStatusCode;
    }
}

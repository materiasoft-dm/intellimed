using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ClientHealthIssueService : IClientHealthIssueService
{
    private readonly HttpClient _httpClient;

    public ClientHealthIssueService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Returns an empty list rather than throwing when the caller lacks the health-issues permission
    // (the API answers 403) — same tolerance the other client-child services show, so a restricted
    // role sees an empty tab instead of an error page.
    public async Task<List<ClientHealthIssueDto>> GetByClientIdAsync(int clientId, bool includeArchived = false)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ClientHealthIssueDto>>(
                $"api/clients/{clientId}/health-issues?includeArchived={includeArchived}") ?? new();
        }
        catch (HttpRequestException)
        {
            return new();
        }
    }

    public async Task<List<ClientHealthIssueDto>> GetSummaryAsync(int clientId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ClientHealthIssueDto>>(
                $"api/clients/{clientId}/health-issues/summary") ?? new();
        }
        catch (HttpRequestException)
        {
            return new();
        }
    }

    public async Task<bool> CreateAsync(CreateClientHealthIssueDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{dto.ClientId}/health-issues", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int clientId, int id, UpdateClientHealthIssueDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{clientId}/health-issues/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ArchiveAsync(int clientId, int id)
    {
        var response = await _httpClient.PostAsync($"api/clients/{clientId}/health-issues/{id}/archive", null);
        return response.IsSuccessStatusCode;
    }
}

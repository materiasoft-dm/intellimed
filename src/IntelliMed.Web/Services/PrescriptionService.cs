using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class PrescriptionService : IPrescriptionService
{
    private readonly HttpClient _httpClient;

    public PrescriptionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<PrescriptionDto>> GetByClientIdAsync(int clientId, bool includeInactive = false)
    {
        var uri = $"api/clients/{clientId}/prescriptions?includeInactive={includeInactive}";
        var results = await _httpClient.GetFromJsonAsync<List<PrescriptionDto>>(uri);
        return results ?? new List<PrescriptionDto>();
    }

    public async Task<MedicationSafetyCheckResultDto?> CheckSafetyAsync(int clientId, CheckPrescriptionSafetyDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{clientId}/prescriptions/check-safety", dto);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<MedicationSafetyCheckResultDto>();
    }

    public async Task<int?> CreateAsync(CreatePrescriptionDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{dto.ClientId}/prescriptions", dto);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        return json!.RootElement.GetProperty("id").GetInt32();
    }

    public async Task<bool> UpdateAsync(int clientId, int id, UpdatePrescriptionDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{clientId}/prescriptions/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> CeaseAsync(int clientId, int id)
    {
        var response = await _httpClient.PostAsync($"api/clients/{clientId}/prescriptions/{id}/cease", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> CancelAsync(int clientId, int id)
    {
        var response = await _httpClient.PostAsync($"api/clients/{clientId}/prescriptions/{id}/cancel", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<PrescriptionIssueDto>> GetIssuesAsync(int clientId, int id)
    {
        var results = await _httpClient.GetFromJsonAsync<List<PrescriptionIssueDto>>($"api/clients/{clientId}/prescriptions/{id}/issues");
        return results ?? new List<PrescriptionIssueDto>();
    }

    public async Task<int?> IssueAsync(int clientId, int id, IssuePrescriptionDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{clientId}/prescriptions/{id}/issue", dto);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        return json!.RootElement.GetProperty("issueId").GetInt32();
    }

    public async Task<PrescriptionIssueDto?> GetIssueByIdAsync(int issueId)
    {
        var response = await _httpClient.GetAsync($"api/prescription-issues/{issueId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PrescriptionIssueDto>();
    }
}

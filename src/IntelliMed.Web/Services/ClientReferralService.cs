using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ClientReferralService : IClientReferralService
{
    private readonly HttpClient _httpClient;

    public ClientReferralService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ClientReferralDto>> GetByClientIdAsync(int clientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<ClientReferralDto>>($"api/clients/{clientId}/referrals")
            ?? Enumerable.Empty<ClientReferralDto>();
    }

    public async Task<bool> CreateAsync(CreateClientReferralDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{dto.ClientId}/referrals", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int clientId, int id, UpdateClientReferralDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{clientId}/referrals/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ArchiveAsync(int clientId, int id)
    {
        var response = await _httpClient.PostAsync($"api/clients/{clientId}/referrals/{id}/archive", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int clientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/clients/{clientId}/referrals/{id}");
        return response.IsSuccessStatusCode;
    }
}

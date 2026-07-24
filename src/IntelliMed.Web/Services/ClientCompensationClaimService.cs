using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ClientCompensationClaimService : IClientCompensationClaimService
{
    private readonly HttpClient _httpClient;

    public ClientCompensationClaimService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ClientCompensationClaimDto>> GetByClientIdAsync(int clientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<ClientCompensationClaimDto>>($"api/clients/{clientId}/compensation-claims")
            ?? Enumerable.Empty<ClientCompensationClaimDto>();
    }

    public async Task<bool> CreateAsync(CreateClientCompensationClaimDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{dto.ClientId}/compensation-claims", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int clientId, int id, UpdateClientCompensationClaimDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{clientId}/compensation-claims/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ArchiveAsync(int clientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/clients/{clientId}/compensation-claims/{id}");
        return response.IsSuccessStatusCode;
    }
}

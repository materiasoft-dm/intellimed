using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ClientAllergyService : IClientAllergyService
{
    private readonly HttpClient _httpClient;

    public ClientAllergyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ClientAllergyDto>> GetByClientIdAsync(int clientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<ClientAllergyDto>>($"api/clients/{clientId}/allergies")
            ?? Enumerable.Empty<ClientAllergyDto>();
    }

    public async Task<bool> CreateAsync(CreateClientAllergyDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{dto.ClientId}/allergies", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int clientId, int id, UpdateClientAllergyDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{clientId}/allergies/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ArchiveAsync(int clientId, int id)
    {
        var response = await _httpClient.PostAsync($"api/clients/{clientId}/allergies/{id}/archive", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int clientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/clients/{clientId}/allergies/{id}");
        return response.IsSuccessStatusCode;
    }
}

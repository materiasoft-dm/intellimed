using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ClientFamilyService : IClientFamilyService
{
    private readonly HttpClient _httpClient;

    public ClientFamilyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ClientFamilyRelationshipDto>> GetByClientIdAsync(int clientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<ClientFamilyRelationshipDto>>($"api/clients/{clientId}/family")
            ?? Enumerable.Empty<ClientFamilyRelationshipDto>();
    }

    public async Task<bool> CreateAsync(CreateClientFamilyRelationshipDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{dto.ClientId}/family", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int clientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/clients/{clientId}/family/{id}");
        return response.IsSuccessStatusCode;
    }
}

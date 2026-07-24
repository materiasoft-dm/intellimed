using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ClientOccupationService : IClientOccupationService
{
    private readonly HttpClient _httpClient;

    public ClientOccupationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ClientOccupationDto>> GetByClientIdAsync(int clientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<ClientOccupationDto>>($"api/clients/{clientId}/occupations")
            ?? Enumerable.Empty<ClientOccupationDto>();
    }

    public async Task<bool> CreateAsync(CreateClientOccupationDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{dto.ClientId}/occupations", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int clientId, int id, UpdateClientOccupationDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{clientId}/occupations/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ArchiveAsync(int clientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/clients/{clientId}/occupations/{id}");
        return response.IsSuccessStatusCode;
    }
}

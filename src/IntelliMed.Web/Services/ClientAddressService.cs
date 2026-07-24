using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ClientAddressService : IClientAddressService
{
    private readonly HttpClient _httpClient;

    public ClientAddressService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ClientAddressDto>> GetByClientIdAsync(int clientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<ClientAddressDto>>($"api/clients/{clientId}/addresses")
            ?? Enumerable.Empty<ClientAddressDto>();
    }

    public async Task<bool> CreateAsync(CreateClientAddressDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{dto.ClientId}/addresses", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int clientId, int id, UpdateClientAddressDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{clientId}/addresses/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int clientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/clients/{clientId}/addresses/{id}");
        return response.IsSuccessStatusCode;
    }
}

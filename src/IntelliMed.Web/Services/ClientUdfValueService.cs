using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ClientUdfValueService : IClientUdfValueService
{
    private readonly HttpClient _httpClient;

    public ClientUdfValueService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ClientUserDefinedFieldValueDto>> GetByClientIdAsync(int clientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<ClientUserDefinedFieldValueDto>>($"api/clients/{clientId}/udf-values")
            ?? Enumerable.Empty<ClientUserDefinedFieldValueDto>();
    }

    public async Task<bool> CreateAsync(CreateClientUserDefinedFieldValueDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/clients/{dto.ClientId}/udf-values", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int clientId, int id, UpdateClientUserDefinedFieldValueDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/clients/{clientId}/udf-values/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int clientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/clients/{clientId}/udf-values/{id}");
        return response.IsSuccessStatusCode;
    }
}

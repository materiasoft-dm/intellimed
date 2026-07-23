using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class PatientAddressService : IPatientAddressService
{
    private readonly HttpClient _httpClient;

    public PatientAddressService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<PatientAddressDto>> GetByPatientIdAsync(int patientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<PatientAddressDto>>($"api/patients/{patientId}/addresses")
            ?? Enumerable.Empty<PatientAddressDto>();
    }

    public async Task<bool> CreateAsync(CreatePatientAddressDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/patients/{dto.PatientId}/addresses", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int patientId, int id, UpdatePatientAddressDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/patients/{patientId}/addresses/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int patientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/patients/{patientId}/addresses/{id}");
        return response.IsSuccessStatusCode;
    }
}

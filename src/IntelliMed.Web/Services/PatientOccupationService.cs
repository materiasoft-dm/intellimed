using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class PatientOccupationService : IPatientOccupationService
{
    private readonly HttpClient _httpClient;

    public PatientOccupationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<PatientOccupationDto>> GetByPatientIdAsync(int patientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<PatientOccupationDto>>($"api/patients/{patientId}/occupations")
            ?? Enumerable.Empty<PatientOccupationDto>();
    }

    public async Task<bool> CreateAsync(CreatePatientOccupationDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/patients/{dto.PatientId}/occupations", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int patientId, int id, UpdatePatientOccupationDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/patients/{patientId}/occupations/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ArchiveAsync(int patientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/patients/{patientId}/occupations/{id}");
        return response.IsSuccessStatusCode;
    }
}

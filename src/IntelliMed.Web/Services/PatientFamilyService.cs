using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class PatientFamilyService : IPatientFamilyService
{
    private readonly HttpClient _httpClient;

    public PatientFamilyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<PatientFamilyRelationshipDto>> GetByPatientIdAsync(int patientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<PatientFamilyRelationshipDto>>($"api/patients/{patientId}/family")
            ?? Enumerable.Empty<PatientFamilyRelationshipDto>();
    }

    public async Task<bool> CreateAsync(CreatePatientFamilyRelationshipDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/patients/{dto.PatientId}/family", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int patientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/patients/{patientId}/family/{id}");
        return response.IsSuccessStatusCode;
    }
}

using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class PatientCompensationClaimService : IPatientCompensationClaimService
{
    private readonly HttpClient _httpClient;

    public PatientCompensationClaimService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<PatientCompensationClaimDto>> GetByPatientIdAsync(int patientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<PatientCompensationClaimDto>>($"api/patients/{patientId}/compensation-claims")
            ?? Enumerable.Empty<PatientCompensationClaimDto>();
    }

    public async Task<bool> CreateAsync(CreatePatientCompensationClaimDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/patients/{dto.PatientId}/compensation-claims", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int patientId, int id, UpdatePatientCompensationClaimDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/patients/{patientId}/compensation-claims/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ArchiveAsync(int patientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/patients/{patientId}/compensation-claims/{id}");
        return response.IsSuccessStatusCode;
    }
}

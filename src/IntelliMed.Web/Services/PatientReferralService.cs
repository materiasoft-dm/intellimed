using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class PatientReferralService : IPatientReferralService
{
    private readonly HttpClient _httpClient;

    public PatientReferralService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<PatientReferralDto>> GetByPatientIdAsync(int patientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<PatientReferralDto>>($"api/patients/{patientId}/referrals")
            ?? Enumerable.Empty<PatientReferralDto>();
    }

    public async Task<bool> CreateAsync(CreatePatientReferralDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/patients/{dto.PatientId}/referrals", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int patientId, int id, UpdatePatientReferralDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/patients/{patientId}/referrals/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ArchiveAsync(int patientId, int id)
    {
        var response = await _httpClient.PostAsync($"api/patients/{patientId}/referrals/{id}/archive", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int patientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/patients/{patientId}/referrals/{id}");
        return response.IsSuccessStatusCode;
    }
}

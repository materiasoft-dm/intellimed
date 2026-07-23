using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class PatientUdfValueService : IPatientUdfValueService
{
    private readonly HttpClient _httpClient;

    public PatientUdfValueService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<PatientUserDefinedFieldValueDto>> GetByPatientIdAsync(int patientId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<PatientUserDefinedFieldValueDto>>($"api/patients/{patientId}/udf-values")
            ?? Enumerable.Empty<PatientUserDefinedFieldValueDto>();
    }

    public async Task<bool> CreateAsync(CreatePatientUserDefinedFieldValueDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/patients/{dto.PatientId}/udf-values", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAsync(int patientId, int id, UpdatePatientUserDefinedFieldValueDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/patients/{patientId}/udf-values/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int patientId, int id)
    {
        var response = await _httpClient.DeleteAsync($"api/patients/{patientId}/udf-values/{id}");
        return response.IsSuccessStatusCode;
    }
}

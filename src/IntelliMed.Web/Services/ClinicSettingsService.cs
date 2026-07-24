using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ClinicSettingsService : IClinicSettingsService
{
    private readonly HttpClient _httpClient;

    public ClinicSettingsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ClinicSettingsDto?> GetAsync()
    {
        var response = await _httpClient.GetAsync("api/clinic-settings");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ClinicSettingsDto>();
    }

    public async Task<bool> UpdateAsync(UpdateClinicSettingsRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync("api/clinic-settings", request);
        return response.IsSuccessStatusCode;
    }
}

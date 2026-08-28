using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private readonly HttpClient _httpClient;

    public SystemSettingsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SystemSettingsDto?> GetAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<SystemSettingsDto>("api/system-settings");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<string?> UpdateAsync(UpdateSystemSettingsDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync("api/system-settings", dto);
        if (response.IsSuccessStatusCode) return null;

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationMessage>();
            if (!string.IsNullOrWhiteSpace(problem?.Message)) return problem.Message;
        }
        catch (Exception)
        {
            // Not a JSON body (403 from the permission check, for instance) — fall through.
        }

        return response.StatusCode == System.Net.HttpStatusCode.Forbidden
            ? "You don't have permission to change system settings."
            : "Save failed. Please try again.";
    }

    private sealed class ValidationMessage
    {
        public string? Message { get; set; }
    }
}

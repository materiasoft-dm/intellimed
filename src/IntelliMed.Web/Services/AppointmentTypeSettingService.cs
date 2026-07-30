using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class AppointmentTypeSettingService : IAppointmentTypeSettingService
{
    private readonly HttpClient _httpClient;

    public AppointmentTypeSettingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<AppointmentTypeSettingDto>> GetAllActiveAsync()
    {
        var results = await _httpClient.GetFromJsonAsync<List<AppointmentTypeSettingDto>>("api/appointment-type-settings");
        return results ?? new List<AppointmentTypeSettingDto>();
    }

    private record CreateResult(int Id);

    public async Task<int?> CreateAsync(CreateAppointmentTypeSettingDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/appointment-type-settings", dto);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<CreateResult>();
            return result?.Id;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create appointment type error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateAsync(int id, UpdateAppointmentTypeSettingDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/appointment-type-settings/{id}", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update appointment type error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ArchiveAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/appointment-type-settings/{id}/archive", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Archive appointment type error: {ex.Message}");
            return false;
        }
    }
}

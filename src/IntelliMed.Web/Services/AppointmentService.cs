using System.Net.Http.Json;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Web.Services;

public class AppointmentService : IAppointmentService
{
    private readonly HttpClient _httpClient;

    public AppointmentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<AppointmentDto>> SearchAppointmentsAsync(AppointmentSearchDto search)
    {
        var args = new List<string>
        {
            $"page={search.Page}",
            $"pageSize={search.PageSize}"
        };

        if (search.ClientId.HasValue) args.Add($"clientId={search.ClientId.Value}");
        if (search.PractitionerId.HasValue) args.Add($"practitionerId={search.PractitionerId.Value}");
        if (search.Status.HasValue) args.Add($"status={search.Status.Value}");
        if (search.FromDate.HasValue) args.Add($"fromDate={search.FromDate.Value:yyyy-MM-dd}");
        if (search.ToDate.HasValue) args.Add($"toDate={search.ToDate.Value:yyyy-MM-dd}");

        var uri = "api/appointments/search?" + string.Join("&", args);
        return await _httpClient.GetFromJsonAsync<PagedResult<AppointmentDto>>(uri)
            ?? new PagedResult<AppointmentDto>();
    }

    public async Task<List<AppointmentDto>> GetWaitingRoomAsync(DateTime? date = null)
    {
        var uri = "api/appointments/waiting-room" + (date.HasValue ? $"?date={date.Value:yyyy-MM-dd}" : "");
        var results = await _httpClient.GetFromJsonAsync<List<AppointmentDto>>(uri);
        return results ?? new List<AppointmentDto>();
    }

    public async Task<AppointmentDto?> GetAppointmentByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/appointments/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AppointmentDto>();
    }

    public async Task<List<AppointmentDto>> GetSeriesAsync(Guid seriesId)
    {
        var results = await _httpClient.GetFromJsonAsync<List<AppointmentDto>>($"api/appointments/series/{seriesId}");
        return results ?? new List<AppointmentDto>();
    }

    public async Task<bool> CheckAvailabilityAsync(int practitionerId, DateTime date, TimeSpan startTime, TimeSpan endTime, int? excludeAppointmentId = null)
    {
        var args = new List<string>
        {
            $"practitionerId={practitionerId}",
            $"date={date:yyyy-MM-dd}",
            $"startTime={startTime}",
            $"endTime={endTime}"
        };
        if (excludeAppointmentId.HasValue) args.Add($"excludeAppointmentId={excludeAppointmentId.Value}");

        try
        {
            return await _httpClient.GetFromJsonAsync<bool>("api/appointments/availability?" + string.Join("&", args));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Check availability error: {ex.Message}");
            return true; // fail open — don't block booking on a transient check failure
        }
    }

    public async Task<ProviderScheduleDayDto?> GetPractitionerScheduleAsync(int practitionerId, DateTime date)
    {
        try
        {
            // A null result comes back as 204 No Content (ASP.NET Core's default behavior for
            // Ok(null)), which has no body to deserialize — handle it before calling ReadFromJsonAsync.
            var response = await _httpClient.GetAsync(
                $"api/appointments/practitioner-schedule?practitionerId={practitionerId}&date={date:yyyy-MM-dd}");
            if (!response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return null;
            return await response.Content.ReadFromJsonAsync<ProviderScheduleDayDto?>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Get practitioner schedule error: {ex.Message}");
            return null;
        }
    }

    private record CreateResult(int Id);

    public async Task<int?> CreateAppointmentAsync(CreateAppointmentDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/appointments", dto);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<CreateResult>();
            return result?.Id;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create appointment error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<int>?> CreateAppointmentSeriesAsync(CreateAppointmentSeriesDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/appointments/series", dto);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<List<int>>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create appointment series error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateAppointmentAsync(int id, UpdateAppointmentDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/appointments/{id}", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update appointment error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateStatusAsync(int id, AppointmentStatus status)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/appointments/{id}/status", new { status });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update appointment status error: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, string? Error)> RescheduleAsync(int id, RescheduleAppointmentDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/appointments/{id}/reschedule", dto);
            if (response.IsSuccessStatusCode) return (true, null);
            var error = await response.Content.ReadAsStringAsync();
            return (false, error);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Reschedule appointment error: {ex.Message}");
            return (false, ex.Message);
        }
    }

    public async Task<bool> DeleteAppointmentAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/appointments/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Delete appointment error: {ex.Message}");
            return false;
        }
    }

    public async Task<int> DeleteSeriesAsync(Guid seriesId, DateTime? fromDate = null)
    {
        try
        {
            var uri = $"api/appointments/series/{seriesId}" + (fromDate.HasValue ? $"?fromDate={fromDate.Value:yyyy-MM-dd}" : "");
            var response = await _httpClient.DeleteAsync(uri);
            if (!response.IsSuccessStatusCode) return 0;
            return await response.Content.ReadFromJsonAsync<int>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Delete appointment series error: {ex.Message}");
            return 0;
        }
    }
}

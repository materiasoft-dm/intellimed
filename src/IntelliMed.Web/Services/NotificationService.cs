using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;

    public NotificationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<NotificationDto>> GetRecentAsync(int take = 20)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<NotificationDto>>($"api/notifications?take={take}")
            ?? Enumerable.Empty<NotificationDto>();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<UnreadCountDto>("api/notifications/unread-count");
        return result?.Count ?? 0;
    }

    public async Task<bool> MarkReadAsync(int id)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/notifications/{id}/read", new { });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> MarkAllReadAsync()
    {
        var response = await _httpClient.PutAsJsonAsync("api/notifications/read-all", new { });
        return response.IsSuccessStatusCode;
    }
}

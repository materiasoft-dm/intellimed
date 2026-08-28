using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class DatabaseBackupService : IDatabaseBackupService
{
    private readonly HttpClient _httpClient;

    public DatabaseBackupService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<DatabaseBackupDto>> GetAllAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<DatabaseBackupDto>>("api/database-backups")
            ?? Enumerable.Empty<DatabaseBackupDto>();
    }

    public async Task<DatabaseBackupSettingsDto?> GetSettingsAsync()
    {
        return await _httpClient.GetFromJsonAsync<DatabaseBackupSettingsDto>("api/database-backups/settings");
    }

    public async Task<bool> UpdateSettingsAsync(UpdateDatabaseBackupSettingsDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync("api/database-backups/settings", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> BackupNowAsync()
    {
        var response = await _httpClient.PostAsync("api/database-backups/backup-now", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<byte[]?> DownloadAsync(string fileName)
    {
        var response = await _httpClient.GetAsync($"api/database-backups/{Uri.EscapeDataString(fileName)}/download");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<bool> DeleteAsync(string fileName)
    {
        var response = await _httpClient.DeleteAsync($"api/database-backups/{Uri.EscapeDataString(fileName)}");
        return response.IsSuccessStatusCode;
    }
}

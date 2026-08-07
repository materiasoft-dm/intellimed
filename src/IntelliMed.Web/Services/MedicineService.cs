using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Web.Services;

public class MedicineService : IMedicineService
{
    private readonly HttpClient _httpClient;

    public MedicineService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<MedicineDto>> SearchAsync(string query, MedicineSource? source = null)
    {
        var uri = $"api/medicines/search?query={Uri.EscapeDataString(query)}";
        if (source.HasValue) uri += $"&source={source.Value}";
        var results = await _httpClient.GetFromJsonAsync<List<MedicineDto>>(uri);
        return results ?? new List<MedicineDto>();
    }

    public async Task<MedicineImportResultDto?> ImportAsync(Stream fileStream, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync("api/medicines/import", content);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<MedicineImportResultDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Import medicines error: {ex.Message}");
            return null;
        }
    }

    public async Task<byte[]?> ExportCsvAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/medicines/export");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Export medicines CSV error: {ex.Message}");
            return null;
        }
    }

    public async Task<SyncProgressDto?> SyncFromSourcesAsync(IProgress<string> progress, Action<string> onJobStarted)
    {
        try
        {
            var startResponse = await _httpClient.PostAsync("api/medicines/sync", null);
            if (!startResponse.IsSuccessStatusCode) return null;

            var startJson = await startResponse.Content.ReadFromJsonAsync<JsonDocument>();
            var jobId = startJson!.RootElement.GetProperty("jobId").GetString()!;
            onJobStarted(jobId);

            var lastLogCount = 0;
            while (true)
            {
                await Task.Delay(500);

                var pollResponse = await _httpClient.GetAsync($"api/medicines/sync/{jobId}/progress");
                if (pollResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    // The job store is in-memory only — a 404 here specifically means the API
                    // restarted mid-run and lost the job, not that the request was unauthorized.
                    // Rethrown below past the generic catch so the UI shows this exact message
                    // instead of the misleading "check your permissions".
                    throw new InvalidOperationException(
                        "The sync job was lost — the server may have restarted while it was running. Please try again.");
                }
                if (!pollResponse.IsSuccessStatusCode) return null;

                var state = await pollResponse.Content.ReadFromJsonAsync<SyncProgressDto>();
                if (state == null) return null;

                for (var i = lastLogCount; i < state.Log.Count; i++)
                {
                    progress.Report(state.Log[i]);
                }
                lastLogCount = state.Log.Count;

                if (state.IsComplete) return state;
            }
        }
        catch (InvalidOperationException)
        {
            throw; // job-lost message from above — let it reach the UI rather than swallowing it
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Sync medicines error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CancelSyncAsync(string jobId)
    {
        var response = await _httpClient.PostAsync($"api/medicines/sync/{jobId}/cancel", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<MedicineDto?> CreateAsync(CreateMedicineDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/medicines", dto);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<MedicineDto>();
    }

    public async Task<bool> UpdateAsync(int id, UpdateMedicineDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/medicines/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeactivateAsync(int id)
    {
        var response = await _httpClient.PostAsync($"api/medicines/{id}/deactivate", null);
        return response.IsSuccessStatusCode;
    }
}

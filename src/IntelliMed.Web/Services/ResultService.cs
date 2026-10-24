using System.Net.Http.Headers;
using System.Net.Http.Json;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Web.Services;

public class ResultService : IResultService
{
    private readonly HttpClient _httpClient;

    public ResultService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<ResultSummaryDto>> SearchAsync(
        ResultStatus? status, string? query, bool abnormalOnly, bool mine, int page, int pageSize, int? clientId = null)
    {
        var args = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (status.HasValue) args.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(query)) args.Add($"query={Uri.EscapeDataString(query)}");
        if (abnormalOnly) args.Add("abnormalOnly=true");
        if (mine) args.Add("mine=true");
        if (clientId.HasValue) args.Add($"clientId={clientId.Value}");

        try
        {
            var result = await _httpClient.GetFromJsonAsync<PagedResult<ResultSummaryDto>>("api/results?" + string.Join("&", args));
            return result ?? new PagedResult<ResultSummaryDto>();
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Search results error: {ex.Message}");
            return new PagedResult<ResultSummaryDto>();
        }
    }

    public async Task<ResultCountsDto> GetCountsAsync(bool mine)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ResultCountsDto>($"api/results/counts?mine={mine.ToString().ToLowerInvariant()}")
                   ?? new ResultCountsDto();
        }
        catch (HttpRequestException)
        {
            return new ResultCountsDto();
        }
    }

    public async Task<ResultDetailDto?> GetAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/results/{id}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ResultDetailDto>() : null;
    }

    public async Task<(IngestResultOutcomeDto? Outcome, string? Error)> UploadAsync(Stream content, string fileName)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var file = new StreamContent(content);
            file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(file, "file", fileName);

            var response = await _httpClient.PostAsync("api/results/upload", form);
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<IngestResultOutcomeDto>(), null);

            var error = (await response.Content.ReadAsStringAsync()).Trim('"');
            return (null, string.IsNullOrWhiteSpace(error) ? "The file could not be uploaded." : error);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Upload result error: {ex.Message}");
            return (null, "The file could not be uploaded.");
        }
    }

    public Task<bool> MatchAsync(int id, int clientId, bool rememberLabIdentifier) =>
        PostAsync($"api/results/{id}/match", new MatchResultDto { ClientId = clientId, RememberLabIdentifier = rememberLabIdentifier });

    public Task<bool> AcknowledgeAsync(int id, string? comment) =>
        PostAsync($"api/results/{id}/acknowledge", new AcknowledgeResultDto { Comment = comment });

    public Task<bool> HoldAsync(int id, string? note) =>
        PostAsync($"api/results/{id}/hold", new HoldResultDto { Note = note });

    public Task<bool> ReopenAsync(int id) => PostAsync($"api/results/{id}/reopen", null);

    public async Task<byte[]?> GetAttachmentAsync(int reportId, int attachmentId)
    {
        var response = await _httpClient.GetAsync($"api/results/{reportId}/attachments/{attachmentId}");
        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
    }

    public async Task<List<ResultIngestKeyDto>> GetIngestKeysAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ResultIngestKeyDto>>("api/results/ingest-keys") ?? new();
        }
        catch (HttpRequestException)
        {
            return new();
        }
    }

    public async Task<CreatedResultIngestKeyDto?> CreateIngestKeyAsync(string label)
    {
        var response = await _httpClient.PostAsJsonAsync("api/results/ingest-keys", new CreateResultIngestKeyDto { Label = label });
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CreatedResultIngestKeyDto>() : null;
    }

    public async Task<bool> RevokeIngestKeyAsync(int id) =>
        (await _httpClient.DeleteAsync($"api/results/ingest-keys/{id}")).IsSuccessStatusCode;

    private async Task<bool> PostAsync(string url, object? body)
    {
        try
        {
            var response = body == null
                ? await _httpClient.PostAsync(url, null)
                : await _httpClient.PostAsJsonAsync(url, body);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Results request error ({url}): {ex.Message}");
            return false;
        }
    }
}

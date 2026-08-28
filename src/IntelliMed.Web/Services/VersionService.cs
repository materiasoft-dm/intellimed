using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class VersionService : IVersionService
{
    private readonly HttpClient _httpClient;

    public VersionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetVersionAsync()
    {
        try
        {
            var dto = await _httpClient.GetFromJsonAsync<VersionDto>("api/version");
            return dto?.Version ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}

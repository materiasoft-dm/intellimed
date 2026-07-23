using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class HealthFundService : IHealthFundService
{
    private readonly HttpClient _httpClient;

    public HealthFundService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<HealthFundDto>> GetAllActiveAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<HealthFundDto>>("api/health-funds")
            ?? Enumerable.Empty<HealthFundDto>();
    }
}

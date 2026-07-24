using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class PractitionerService : IPractitionerService
{
    private readonly HttpClient _httpClient;

    public PractitionerService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<PractitionerDto>> GetAllActiveAsync()
    {
        var results = await _httpClient.GetFromJsonAsync<List<PractitionerDto>>("api/practitioners/active");
        return results ?? new List<PractitionerDto>();
    }
}

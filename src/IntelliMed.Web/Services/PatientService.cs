using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class PatientService : IPatientService
{
    private readonly HttpClient _httpClient;

    public PatientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<PatientDto>> SearchPatientsAsync(PatientSearchDto search)
    {
        var args = new List<string>
        {
            $"page={search.Page}",
            $"pageSize={search.PageSize}"
        };

        if (!string.IsNullOrWhiteSpace(search.Query))
            args.Add($"query={Uri.EscapeDataString(search.Query)}");

        var uri = "api/patients/search?" + string.Join("&", args);
        return await _httpClient.GetFromJsonAsync<PagedResult<PatientDto>>(uri)
            ?? new PagedResult<PatientDto>();
    }

    public async Task<bool> CreatePatientAsync(CreatePatientDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/patients", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create patient error: {ex.Message}");
            return false;
        }
    }
}

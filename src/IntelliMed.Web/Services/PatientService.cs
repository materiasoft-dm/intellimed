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

    private record CreateResult(int Id);

    public async Task<int?> CreatePatientAsync(CreatePatientDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/patients", dto);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<CreateResult>();
            return result?.Id;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create patient error: {ex.Message}");
            return null;
        }
    }

    public async Task<PatientDto?> GetPatientByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/patients/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PatientDto>();
    }

    public async Task<bool> UpdatePatientAsync(int id, UpdatePatientDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/patients/{id}", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update patient error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ArchivePatientAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/patients/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Archive patient error: {ex.Message}");
            return false;
        }
    }
}

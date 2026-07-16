using System.Net.Http.Json;
using IntelliMed.Core.DTOs;
using IntelliMed.UI.Services;

namespace IntelliMed.Web.Services;

public interface IPatientService
{
    Task<bool> CreatePatientAsync(CreatePatientDto dto);
    // Additional methods (Get, Update, Delete) can be added later
}

public class PatientService : IPatientService
{
    private readonly HttpClient _httpClient;

    public PatientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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

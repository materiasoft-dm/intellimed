using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ClinicService : IClinicService
{
    private readonly HttpClient _httpClient;

    public ClinicService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<MyClinicDto>?> GetMyClinicsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<MyClinicDto>>("api/clinics/my-clinics");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetMyClinics error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<MyClinicDto>?> GetLookupAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<MyClinicDto>>("api/clinics/lookup");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetClinicLookup error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ClinicDto>?> GetAllAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ClinicDto>>("api/clinics");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetAllClinics error: {ex.Message}");
            return null;
        }
    }

    public async Task<ClinicDto?> GetByIdAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ClinicDto>($"api/clinics/{id}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetClinic error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CreateAsync(CreateClinicDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/clinics", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CreateClinic error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateAsync(int id, UpdateClinicDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/clinics/{id}", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"UpdateClinic error: {ex.Message}");
            return false;
        }
    }

    public async Task<List<ClinicUserDto>?> GetClinicUsersAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ClinicUserDto>>($"api/clinics/{id}/users");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetClinicUsers error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> SetClinicUsersAsync(int id, SetClinicUsersRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/clinics/{id}/users", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SetClinicUsers error: {ex.Message}");
            return false;
        }
    }
}

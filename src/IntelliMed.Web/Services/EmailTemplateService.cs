using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private readonly HttpClient _httpClient;

    public EmailTemplateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<EmailTemplateDto>?> GetAllAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<EmailTemplateDto>>("api/email-templates");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetAll email templates error: {ex.Message}");
            return null;
        }
    }

    public async Task<EmailTemplateDto?> GetByIdAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/email-templates/{id}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<EmailTemplateDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetById email template error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CreateAsync(SaveEmailTemplateRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/email-templates", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create email template error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateAsync(int id, SaveEmailTemplateRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/email-templates/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update email template error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ArchiveAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/email-templates/{id}/archive", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Archive email template error: {ex.Message}");
            return false;
        }
    }
}

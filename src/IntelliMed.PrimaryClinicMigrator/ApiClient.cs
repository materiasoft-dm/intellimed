using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntelliMed.PrimaryClinicMigrator;

/// <summary>
/// Sends extracted legacy data to the IntelliMed Web API for import. Authenticates via the same
/// JWT bearer login every other IntelliMed client uses (POST /api/auth/login) — the API has no
/// separate API-key mechanism, so a static key header would never get past the global
/// RequireAuthenticatedUser() fallback policy.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(10)
        };
    }

    /// <summary>Logs in and attaches the returned JWT as a Bearer token for all subsequent requests.</summary>
    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode)
            return (false, $"Login request failed with status {response.StatusCode}.");

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        if (result == null || !result.Success || string.IsNullOrWhiteSpace(result.Token))
            return (false, result?.ErrorMessage ?? "Login failed — check the email/password.");

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Token);
        return (true, null);
    }

    /// <summary>POSTs a batch of rows to a specific import endpoint.</summary>
    public async Task<ImportResult> PostImportAsync(string endpoint, List<Dictionary<string, object>> rows, int clinicId)
    {
        var payload = new { clinicId, rows };

        var response = await _http.PostAsJsonAsync(endpoint, payload, JsonOptions);
        await EnsureSuccessWithBodyAsync(response);

        var result = await response.Content.ReadFromJsonAsync<ImportResult>(JsonOptions);
        return result ?? new ImportResult();
    }

    /// <summary>POSTs payment allocations and payments to the import-payments endpoint.</summary>
    public async Task<ImportResult> PostPaymentsImportAsync(
        List<Dictionary<string, object>> allocations,
        List<Dictionary<string, object>> payments,
        int clinicId)
    {
        var payload = new { clinicId, allocations, payments };

        var response = await _http.PostAsJsonAsync("api/primary-clinic-migrator/import-payments", payload, JsonOptions);
        await EnsureSuccessWithBodyAsync(response);

        var result = await response.Content.ReadFromJsonAsync<ImportResult>(JsonOptions);
        return result ?? new ImportResult();
    }

    /// <summary>Like response.EnsureSuccessStatusCode(), but includes the response body in the
    /// exception message — otherwise a 400 from a bad row just shows up as a bare status code.</summary>
    private static async Task EnsureSuccessWithBodyAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"Request failed with status {response.StatusCode}: {body}");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private class LoginResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

public class ImportResult
{
    public int ClientsCreated { get; set; }
    public int ClientsUpdated { get; set; }
    public int AddressesCreated { get; set; }
    public int AddressesUpdated { get; set; }
    public int ReferralsCreated { get; set; }
    public int ReferralsUpdated { get; set; }
    public int OccupationsCreated { get; set; }
    public int OccupationsUpdated { get; set; }
    public int FamilyRelationshipsCreated { get; set; }
    public int FamilyRelationshipsUpdated { get; set; }
    public int CompensationClaimsCreated { get; set; }
    public int CompensationClaimsUpdated { get; set; }
    public int InvoicesCreated { get; set; }
    public int InvoicesUpdated { get; set; }
    public int InvoiceItemsCreated { get; set; }
    public int InvoiceItemsUpdated { get; set; }
    public int PaymentsCreated { get; set; }
    public int PaymentsUpdated { get; set; }
    public List<string> Warnings { get; set; } = new();
}

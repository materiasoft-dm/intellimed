using IntelliMed.UI.Services;

namespace IntelliMed.Web.Services;

public class ClinicContextService : IClinicContextService
{
    private const string ClinicIdKey = "intellimed_current_clinic_id";
    private const string HeaderName = "X-Clinic-Id";

    private readonly HttpClient _httpClient;
    private readonly IClientStorage _storage;

    public ClinicContextService(HttpClient httpClient, IClientStorage storage)
    {
        _httpClient = httpClient;
        _storage = storage;
    }

    public async Task<int?> GetCurrentClinicIdAsync()
    {
        var value = await _storage.GetItemAsync(ClinicIdKey);
        return int.TryParse(value, out var id) ? id : null;
    }

    public async Task SetCurrentClinicIdAsync(int clinicId)
    {
        await _storage.SetItemAsync(ClinicIdKey, clinicId.ToString());
        ApplyHeader(clinicId);
    }

    public async Task RestoreAsync()
    {
        var clinicId = await GetCurrentClinicIdAsync();
        if (clinicId.HasValue)
        {
            ApplyHeader(clinicId.Value);
        }
    }

    public async Task ClearAsync()
    {
        await _storage.SetItemAsync(ClinicIdKey, null);
        _httpClient.DefaultRequestHeaders.Remove(HeaderName);
    }

    private void ApplyHeader(int clinicId)
    {
        _httpClient.DefaultRequestHeaders.Remove(HeaderName);
        _httpClient.DefaultRequestHeaders.Add(HeaderName, clinicId.ToString());
    }
}

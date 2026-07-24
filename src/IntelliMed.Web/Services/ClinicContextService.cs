using IntelliMed.UI.Services;

namespace IntelliMed.Web.Services;

/// <summary>
/// Tracks which clinic the current user is working in, in local storage. The X-Clinic-Id
/// header itself is attached per-request by AuthHeaderHandler (reading the same storage key),
/// so every API call — regardless of which page or service makes it — is always scoped
/// consistently without relying on load ordering between components.
/// </summary>
public class ClinicContextService : IClinicContextService
{
    private const string ClinicIdKey = "intellimed_current_clinic_id";

    private readonly IClientStorage _storage;

    public ClinicContextService(IClientStorage storage)
    {
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
    }

    public async Task ClearAsync()
    {
        await _storage.SetItemAsync(ClinicIdKey, null);
    }
}

namespace IntelliMed.Web.Services;

/// <summary>
/// Tracks which clinic the current user is currently working in and makes sure every
/// subsequent HTTP request carries it (via the X-Clinic-Id header), so the API can scope
/// patient/appointment/invoice data to that clinic.
/// </summary>
public interface IClinicContextService
{
    Task<int?> GetCurrentClinicIdAsync();
    Task SetCurrentClinicIdAsync(int clinicId);

    /// <summary>Clears the stored clinic and removes the header — used when the stored clinic no longer
    /// belongs to the signed-in user (e.g. a different user logged in on the same browser).</summary>
    Task ClearAsync();

    /// <summary>Re-attaches the X-Clinic-Id header from local storage. Call once per app load, early.</summary>
    Task RestoreAsync();
}

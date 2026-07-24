using System.Net.Http.Headers;
using IntelliMed.UI.Services;

namespace IntelliMed.Web.Services;

/// <summary>
/// DelegatingHandler that attaches the JWT bearer token and the current clinic (X-Clinic-Id)
/// from IClientStorage to every outgoing HttpClient request. Reading storage fresh on every
/// request (rather than setting a header once at startup) avoids a race where a page's own
/// data fetch can run before the app has finished restoring the signed-in user's context.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly IClientStorage _storage;
    private const string TokenKey = "intellimed_token";
    private const string ClinicIdKey = "intellimed_current_clinic_id";
    private const string ClinicHeaderName = "X-Clinic-Id";

    public AuthHeaderHandler(IClientStorage storage)
    {
        _storage = storage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await _storage.GetItemAsync(TokenKey);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var clinicId = await _storage.GetItemAsync(ClinicIdKey);
            if (!string.IsNullOrEmpty(clinicId))
            {
                request.Headers.Remove(ClinicHeaderName);
                request.Headers.Add(ClinicHeaderName, clinicId);
            }
        }
        catch
        {
            // If storage fails, proceed without auth/clinic headers
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

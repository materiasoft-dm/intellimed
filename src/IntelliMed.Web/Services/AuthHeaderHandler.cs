using System.Net;
using System.Net.Http.Headers;
using IntelliMed.UI.Services;
using Microsoft.AspNetCore.Components;

namespace IntelliMed.Web.Services;

/// <summary>
/// DelegatingHandler that attaches the JWT bearer token and the current clinic (X-Clinic-Id)
/// from IClientStorage to every outgoing HttpClient request. Reading storage fresh on every
/// request (rather than setting a header once at startup) avoids a race where a page's own
/// data fetch can run before the app has finished restoring the signed-in user's context.
/// Also catches an expired/invalid token: a stored token satisfies MainLayout's presence-only
/// login guard even after the server has stopped honoring it, which would otherwise leave the
/// page silently broken (every fetch 401ing) instead of bounced back to /login.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly IClientStorage _storage;
    private readonly NavigationManager _navigation;
    private const string TokenKey = "intellimed_token";
    private const string ClinicIdKey = "intellimed_current_clinic_id";
    private const string ClinicHeaderName = "X-Clinic-Id";

    public AuthHeaderHandler(IClientStorage storage, NavigationManager navigation)
    {
        _storage = storage;
        _navigation = navigation;
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

        var response = await base.SendAsync(request, cancellationToken);

        // A 401 on the login endpoint itself is a normal wrong-password attempt, handled by
        // Login.razor's own error UI — must not trigger a redirect. Any other 401 means the
        // stored token is missing/expired/invalid server-side, so clear it and bounce to login.
        var isLoginRequest = request.RequestUri?.AbsolutePath.EndsWith("api/auth/login", StringComparison.OrdinalIgnoreCase) == true;
        if (response.StatusCode == HttpStatusCode.Unauthorized && !isLoginRequest)
        {
            await _storage.SetItemAsync(TokenKey, "");
            _navigation.NavigateTo("/login", forceLoad: true);
        }

        return response;
    }
}

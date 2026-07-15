using System.Net.Http.Headers;
using IntelliMed.UI.Services;

namespace IntelliMed.Web.Services;

/// <summary>
/// DelegatingHandler that attaches the JWT bearer token from IClientStorage
/// to every outgoing HttpClient request. This ensures all API calls are authenticated
/// regardless of which service makes the call.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly IClientStorage _storage;
    private const string TokenKey = "intellimed_token";

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
        }
        catch
        {
            // If storage fails, proceed without auth header
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

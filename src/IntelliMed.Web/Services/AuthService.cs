using System.Security.Cryptography;
using System.Text;
using IntelliMed.UI.Services;

namespace IntelliMed.Web.Services;

public interface IAuthService
{
    Task<bool> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    string? GetCurrentUserEmail();
}

public class AuthService : IAuthService
{
    private readonly IClientStorage _storage;
    private const string AuthKey = "intellimed_auth";
    private const string UserEmailKey = "intellimed_user_email";

    // Default user credentials for demo
    private const string DefaultEmail = "admin@clinic.com";
    private const string DefaultPassword = "IntelliMed2024!";

    public AuthService(IClientStorage storage)
    {
        _storage = storage;
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        // Simple hardcoded authentication for demo purposes
        // In production, this would call an API endpoint
        if (email.Equals(DefaultEmail, StringComparison.OrdinalIgnoreCase) && 
            password == DefaultPassword)
        {
            // Store authentication state
            await _storage.SetItemAsync(AuthKey, "true");
            await _storage.SetItemAsync(UserEmailKey, email);
            return true;
        }

        return false;
    }

    public async Task LogoutAsync()
    {
        await _storage.SetItemAsync(AuthKey, "");
        await _storage.SetItemAsync(UserEmailKey, "");
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var authValue = await _storage.GetItemAsync(AuthKey);
        return authValue == "true";
    }

    public string? GetCurrentUserEmail()
    {
        return _storage.GetItemAsync(UserEmailKey).GetAwaiter().GetResult();
    }
}
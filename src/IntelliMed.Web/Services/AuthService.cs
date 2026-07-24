using System.Net.Http.Json;
using System.Text.Json;
using IntelliMed.Core.DTOs;
using IntelliMed.UI.Services;

namespace IntelliMed.Web.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    string? GetCurrentUserEmail();
    string? GetCurrentUserName();
    string? GetToken();
    Task<CurrentUserResponse?> GetCurrentUserAsync();
    Task<UserProfileDto?> GetMyProfileAsync();
    Task<bool> UpdateMyProfileAsync(UpdateProfileRequest request);
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IClientStorage _storage;
    private const string TokenKey = "intellimed_token";
    private const string UserEmailKey = "intellimed_user_email";
    private const string UserNameKey = "intellimed_user_name";

    public AuthService(HttpClient httpClient, IClientStorage storage)
    {
        _httpClient = httpClient;
        _storage = storage;
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        try
        {
            var request = new LoginRequest
            {
                Email = email,
                Password = password,
                RememberMe = true
            };

            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

                if (result?.Success == true && !string.IsNullOrEmpty(result.Token))
                {
                    // Store authentication state
                    await _storage.SetItemAsync(TokenKey, result.Token);
                    await _storage.SetItemAsync(UserEmailKey, result.Email ?? "");
                    await _storage.SetItemAsync(UserNameKey, result.FullName ?? "");

                    // Set token for future requests
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.Token);
                }

                return result;
            }

            // Handle error response
            var errorResult = await response.Content.ReadFromJsonAsync<LoginResponse>();
            return errorResult ?? new LoginResponse { Success = false, ErrorMessage = "Login failed" };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Login error: {ex.Message}");
            return new LoginResponse { Success = false, ErrorMessage = "Connection error. Please try again." };
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            // Call logout endpoint (fire and forget - we clear local state regardless)
            await _httpClient.PostAsync("api/auth/logout", null);
        }
        catch
        {
            // Ignore logout API errors - clear local state anyway
        }
        finally
        {
            // Clear local storage
            await _storage.SetItemAsync(TokenKey, "");
            await _storage.SetItemAsync(UserEmailKey, "");
            await _storage.SetItemAsync(UserNameKey, "");

            // Remove authorization header
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _storage.GetItemAsync(TokenKey);
        return !string.IsNullOrEmpty(token);
    }

    public string? GetCurrentUserEmail()
    {
        return _storage.GetItemAsync(UserEmailKey).GetAwaiter().GetResult();
    }

    public string? GetCurrentUserName()
    {
        return _storage.GetItemAsync(UserNameKey).GetAwaiter().GetResult();
    }

    public string? GetToken()
    {
        return _storage.GetItemAsync(TokenKey).GetAwaiter().GetResult();
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync()
    {
        try
        {
            // Ensure token is set
            var token = await _storage.GetItemAsync(TokenKey);
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.GetAsync("api/auth/me");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
            }

            return new CurrentUserResponse { IsAuthenticated = false };
        }
        catch
        {
            return new CurrentUserResponse { IsAuthenticated = false };
        }
    }

    public async Task<UserProfileDto?> GetMyProfileAsync()
    {
        try
        {
            await AttachTokenAsync();
            var response = await _httpClient.GetAsync("api/auth/me/profile");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<UserProfileDto>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetMyProfile error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateMyProfileAsync(UpdateProfileRequest request)
    {
        try
        {
            await AttachTokenAsync();
            var response = await _httpClient.PutAsJsonAsync("api/auth/me/profile", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"UpdateMyProfile error: {ex.Message}");
            return false;
        }
    }

    private async Task AttachTokenAsync()
    {
        var token = await _storage.GetItemAsync(TokenKey);
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }
}
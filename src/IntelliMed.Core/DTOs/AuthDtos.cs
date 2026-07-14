using System.ComponentModel.DataAnnotations;

namespace IntelliMed.Core.DTOs;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public IList<string>? Roles { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class LogoutResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class CurrentUserResponse
{
    public bool IsAuthenticated { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public IList<string>? Roles { get; set; }
}
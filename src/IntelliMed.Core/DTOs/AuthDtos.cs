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

/// <summary>
/// The current user's own editable profile — shown on the Profile Settings page.
/// </summary>
public class UserProfileDto
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();

    public string? Title { get; set; }
    public string? MiddleName { get; set; }
    public string? MobilePhone { get; set; }
    public string? BusinessHoursPhone { get; set; }
    public string? Fax { get; set; }
    public string? Qualifications { get; set; }
    public string? Specialty { get; set; }
    public string? ProviderNumber { get; set; }
    public string? AhpraNumber { get; set; }
    public string? HpiiNumber { get; set; }
    public string? Note { get; set; }
    public bool VocationallyRegistered { get; set; }
    public bool InternalProvider { get; set; }
    public bool EPrescribingEnabled { get; set; }

    public int? GroupId { get; set; }
    public string? GroupName { get; set; }

    public string? ResidentialAddress { get; set; }
    public string? ResidentialSuburb { get; set; }
    public string? ResidentialPostcode { get; set; }
    public string? ResidentialState { get; set; }

    public bool PostalSameAsResidential { get; set; } = true;
    public string? PostalAddress { get; set; }
    public string? PostalSuburb { get; set; }
    public string? PostalPostcode { get; set; }
    public string? PostalState { get; set; }
}

/// <summary>
/// Request to update the current user's own profile. Email/roles are not editable here.
/// </summary>
public class UpdateProfileRequest
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string? MiddleName { get; set; }
    public string? MobilePhone { get; set; }
    public string? BusinessHoursPhone { get; set; }
    public string? Fax { get; set; }
    public string? Qualifications { get; set; }
    public string? Specialty { get; set; }
    public string? ProviderNumber { get; set; }
    public string? AhpraNumber { get; set; }
    public string? HpiiNumber { get; set; }
    public string? Note { get; set; }
    public bool VocationallyRegistered { get; set; }
    public bool InternalProvider { get; set; }
    public bool EPrescribingEnabled { get; set; }

    public int? GroupId { get; set; }

    public string? ResidentialAddress { get; set; }
    public string? ResidentialSuburb { get; set; }
    public string? ResidentialPostcode { get; set; }
    public string? ResidentialState { get; set; }

    public bool PostalSameAsResidential { get; set; } = true;
    public string? PostalAddress { get; set; }
    public string? PostalSuburb { get; set; }
    public string? PostalPostcode { get; set; }
    public string? PostalState { get; set; }
}
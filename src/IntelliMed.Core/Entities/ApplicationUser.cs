using Microsoft.AspNetCore.Identity;

namespace IntelliMed.Core.Entities;

/// <summary>
/// Application user extending ASP.NET Identity's IdentityUser.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Profile — personal/professional info the user can edit about themselves.
    // Provider/AHPRA/HPI-I are only meaningful for doctors, but kept optional for any user.
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
    public ProviderGroup? Group { get; set; }

    public string? ResidentialAddress { get; set; }
    public string? ResidentialSuburb { get; set; }
    public string? ResidentialPostcode { get; set; }
    public string? ResidentialState { get; set; }

    public bool PostalSameAsResidential { get; set; } = true;
    public string? PostalAddress { get; set; }
    public string? PostalSuburb { get; set; }
    public string? PostalPostcode { get; set; }
    public string? PostalState { get; set; }

    /// <summary>
    /// Gets the full name of the user.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();
}
using System.ComponentModel.DataAnnotations;

namespace IntelliMed.Core.DTOs;

public class ClinicDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Abn { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Suburb { get; set; }
    public string? Postcode { get; set; }
    public string? State { get; set; }
    public bool IsActive { get; set; }
    public int UserCount { get; set; }
}

/// <summary>
/// Minimal shape for the clinic switcher dropdown — only what's needed to list and switch.
/// </summary>
public class MyClinicDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CreateClinicDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Abn { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Suburb { get; set; }
    public string? Postcode { get; set; }
    public string? State { get; set; }
}

public class UpdateClinicDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Abn { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Suburb { get; set; }
    public string? Postcode { get; set; }
    public string? State { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A user shown in the Clinic Manager's "assign users" list, with whether they're currently assigned.
/// </summary>
public class ClinicUserDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
}

public class SetClinicUsersRequest
{
    public IList<string> UserIds { get; set; } = new List<string>();
}

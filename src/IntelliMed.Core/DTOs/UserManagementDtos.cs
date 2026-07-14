using System.ComponentModel.DataAnnotations;

namespace IntelliMed.Core.DTOs;

/// <summary>
/// Represents a user in the user management list.
/// </summary>
public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// Request to create a new user with optional role assignments.
/// </summary>
public class CreateUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Roles to assign to the new user. Defaults to empty (no roles).
    /// </summary>
    public IList<string> Roles { get; set; } = new List<string>();
}

/// <summary>
/// Request to update an existing user's profile and status.
/// </summary>
public class UpdateUserRequest
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// If provided, replaces the user's current roles entirely.
    /// If null, roles are left unchanged.
    /// </summary>
    public IList<string>? Roles { get; set; }
}

/// <summary>
/// Request to change a user's password (admin-initiated reset).
/// </summary>
public class ResetPasswordRequest
{
    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Represents a role with its description and associated permissions.
/// </summary>
public class RoleDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IList<string> Permissions { get; set; } = new List<string>();
}

/// <summary>
/// Request to assign specific roles to a user.
/// </summary>
public class AssignRolesRequest
{
    [Required]
    public IList<string> Roles { get; set; } = new List<string>();
}

/// <summary>
/// Generic API response wrapper for user management operations.
/// </summary>
public class UserManagementResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public UserDto? User { get; set; }
}

/// <summary>
/// Represents a page that can be assigned to a role via the permission system.
/// </summary>
public class PageDefinitionDto
{
    /// <summary>
    /// Unique key for the page (e.g., "patients", "admin/users").
    /// </summary>
    public string PageKey { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the page (e.g., "Patient Records", "User Management").
    /// </summary>
    public string PageName { get; set; } = string.Empty;

    /// <summary>
    /// Category grouping (e.g., "Clinical", "Admin", "Financial").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what the page does.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Represents the permissions assigned to a specific role — which pages it can access.
/// </summary>
public class RolePermissionsDto
{
    public string RoleName { get; set; } = string.Empty;
    public IList<string> PageKeys { get; set; } = new List<string>();
}

/// <summary>
/// Request to save page permissions for a role.
/// </summary>
public class SaveRolePermissionsRequest
{
    public IList<string> PageKeys { get; set; } = new List<string>();
}
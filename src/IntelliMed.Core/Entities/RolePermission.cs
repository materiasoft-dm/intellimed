namespace IntelliMed.Core.Entities;

/// <summary>
/// Maps a role to a specific page permission. Each record grants a role access to one page.
/// </summary>
public class RolePermission
{
    public int Id { get; set; }

    /// <summary>
    /// The role name (e.g., "Manager", "Doctor").
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Unique key identifying the page (e.g., "patients", "admin/users").
    /// </summary>
    public string PageKey { get; set; } = string.Empty;

    /// <summary>
    /// Category the page belongs to (e.g., "Clinical", "Admin", "Financial").
    /// </summary>
    public string Category { get; set; } = string.Empty;
}
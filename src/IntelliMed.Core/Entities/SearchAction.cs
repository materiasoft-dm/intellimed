namespace IntelliMed.Core.Entities;

/// <summary>What happens when a search action is activated.</summary>
public enum SearchActionType
{
    /// <summary>Navigates to <see cref="SearchAction.Target"/> (a route path).</summary>
    Navigate = 0,

    /// <summary>Opens a modal identified by <see cref="SearchAction.Target"/>. No built-in handlers yet — falls back to showing the description.</summary>
    Modal = 1,

    /// <summary>Display-only — shows the description, no navigation or modal.</summary>
    Info = 2
}

/// <summary>
/// A single entry in the global command palette (floating search button). Data-driven so the
/// catalogue can grow without code changes — see the admin CRUD pages under /admin/search-actions.
/// </summary>
public class SearchAction
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Free-text synonym terms (e.g. "patient create new register") that also match this action.</summary>
    public string? Keywords { get; set; }

    /// <summary>Subtitle shown under the title in search results.</summary>
    public string? Description { get; set; }

    public string Category { get; set; } = string.Empty;
    public SearchActionType ActionType { get; set; } = SearchActionType.Navigate;

    /// <summary>Route path for <see cref="SearchActionType.Navigate"/>, or a modal identifier for <see cref="SearchActionType.Modal"/>.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Ties this action to the RolePermissions page-key catalogue for RBAC filtering. Null means visible to every authenticated user.</summary>
    public string? PageKey { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

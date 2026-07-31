using System.ComponentModel.DataAnnotations;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class SearchActionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Keywords { get; set; }
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public SearchActionType ActionType { get; set; }
    public string Target { get; set; } = string.Empty;
    public string? PageKey { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class SaveSearchActionRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Keywords { get; set; }
    public string? Description { get; set; }

    [Required]
    public string Category { get; set; } = string.Empty;

    public SearchActionType ActionType { get; set; } = SearchActionType.Navigate;

    [Required]
    public string Target { get; set; } = string.Empty;

    public string? PageKey { get; set; }
    public int SortOrder { get; set; }
}

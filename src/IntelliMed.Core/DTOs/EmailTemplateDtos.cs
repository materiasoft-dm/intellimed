using System.ComponentModel.DataAnnotations;

namespace IntelliMed.Core.DTOs;

public class EmailTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? EventKey { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveEmailTemplateRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>System event this template fires for, e.g. "InviteEmail" / "ForgotPassword". Null leaves it unassigned.</summary>
    public string? EventKey { get; set; }
}

namespace IntelliMed.Core.Entities;

/// <summary>
/// A clinic-authored email template, optionally assigned to a fixed system event
/// (see <see cref="EmailEventKeys"/>). At most one non-archived template can hold
/// a given event key per clinic at a time.
/// </summary>
public class EmailTemplate
{
    public int Id { get; set; }
    public int ClinicId { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? EventKey { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>System events an email template can be assigned to.</summary>
public static class EmailEventKeys
{
    public const string InviteEmail = "InviteEmail";
    public const string ForgotPassword = "ForgotPassword";

    public static readonly IReadOnlyList<string> All = new[] { InviteEmail, ForgotPassword };

    public static string DisplayName(string eventKey) => eventKey switch
    {
        InviteEmail => "Invite Email",
        ForgotPassword => "Forgot Password",
        _ => eventKey
    };
}

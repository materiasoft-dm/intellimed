namespace IntelliMed.Core.Entities;

/// <summary>
/// Single-row settings table holding practice-wide identity/contact info.
/// </summary>
public class ClinicSettings
{
    public int Id { get; set; }
    public string PracticeName { get; set; } = string.Empty;
    public string? Abn { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? Suburb { get; set; }
    public string? Postcode { get; set; }
    public string? State { get; set; }

    /// <summary>Default appointment calendar slot granularity, in minutes. Users can override this in their own Profile Settings.</summary>
    public int MinimumTimeslotMinutes { get; set; } = 15;

    public bool SmtpEnabled { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFromEmail { get; set; }
    public string? SmtpFromName { get; set; }
    public bool SmtpUseSsl { get; set; } = true;
}

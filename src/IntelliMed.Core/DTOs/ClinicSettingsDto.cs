using System.ComponentModel.DataAnnotations;

namespace IntelliMed.Core.DTOs;

public class ClinicSettingsDto
{
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
    public int MinimumTimeslotMinutes { get; set; }

    public bool SmtpEnabled { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFromEmail { get; set; }
    public string? SmtpFromName { get; set; }
    public bool SmtpUseSsl { get; set; } = true;
}

public class UpdateClinicSettingsRequest
{
    [Required]
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
    [Range(1, 240)]
    public int MinimumTimeslotMinutes { get; set; } = 15;

    public bool SmtpEnabled { get; set; }
    public string? SmtpHost { get; set; }
    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFromEmail { get; set; }
    public string? SmtpFromName { get; set; }
    public bool SmtpUseSsl { get; set; } = true;
}

/// <summary>Sends a one-off test email using the clinic's current (possibly unsaved-to-DB-yet) SMTP settings.</summary>
public class TestEmailRequest
{
    [Required]
    [EmailAddress]
    public string ToEmail { get; set; } = string.Empty;

    public string Message { get; set; } = "This is a test email";
}

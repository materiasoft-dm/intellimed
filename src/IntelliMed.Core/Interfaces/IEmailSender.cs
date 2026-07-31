namespace IntelliMed.Core.Interfaces;

/// <summary>Sends email using the clinic's configured SMTP settings. No-ops with an error message when SMTP isn't configured.</summary>
public interface IEmailSender
{
    Task<(bool Success, string? Error)> SendAsync(string toEmail, string subject, string htmlBody);
}

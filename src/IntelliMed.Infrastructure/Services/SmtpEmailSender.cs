using System.Net;
using System.Net.Mail;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliMed.Infrastructure.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly AppDbContext _context;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(AppDbContext context, ILogger<SmtpEmailSender> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error)> SendAsync(string toEmail, string subject, string htmlBody)
    {
        var settings = await _context.ClinicSettings.SingleAsync(s => s.Id == 1);

        if (!settings.SmtpEnabled || string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            _logger.LogWarning("Email not sent to {To}: SMTP is not configured/enabled.", toEmail);
            return (false, "SMTP is not configured. Set it up in Clinic Settings first.");
        }

        var fromAddress = !string.IsNullOrWhiteSpace(settings.SmtpFromEmail)
            ? settings.SmtpFromEmail
            : !string.IsNullOrWhiteSpace(settings.SmtpUsername)
                ? settings.SmtpUsername
                : "no-reply@intellimed.local";

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress, string.IsNullOrWhiteSpace(settings.SmtpFromName) ? settings.PracticeName : settings.SmtpFromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.SmtpUseSsl,
                Credentials = string.IsNullOrWhiteSpace(settings.SmtpUsername)
                    ? null
                    : new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword)
            };

            await client.SendMailAsync(message);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", toEmail);
            return (false, ex.Message);
        }
    }
}

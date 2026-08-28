using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace IntelliMed.Infrastructure.Services;

/// <summary>The only IPrescriptionTransmissionService implementation today — always declines, same
/// no-op-gracefully shape as SmtpEmailSender when SMTP isn't configured. Real eRx transmission is a
/// future integration, not something to fake or partially implement here.</summary>
public class NotConfiguredPrescriptionTransmissionService : IPrescriptionTransmissionService
{
    private readonly ILogger<NotConfiguredPrescriptionTransmissionService> _logger;

    public NotConfiguredPrescriptionTransmissionService(ILogger<NotConfiguredPrescriptionTransmissionService> logger)
    {
        _logger = logger;
    }

    public Task<(bool Success, string? Error)> TransmitAsync(Prescription prescription, PrescriptionIssue issue)
    {
        _logger.LogWarning(
            "eRx transmission requested for prescription {PrescriptionId} (script {ScriptNumber}) but no transmission provider is configured.",
            prescription.Id, prescription.ScriptNumber);
        return Task.FromResult<(bool, string?)>((false, "Electronic prescribing (eRx) is not configured for this practice."));
    }
}

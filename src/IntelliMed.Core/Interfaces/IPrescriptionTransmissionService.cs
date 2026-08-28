using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

/// <summary>The eRx extension seam — deliberately not implemented today. Real electronic prescribing
/// (transmission to Australia's eRx network) requires becoming a certified/registered prescribing-
/// software vendor with a paid third-party network, a real business relationship this codebase doesn't
/// have yet. Mirrors IEmailSender's shape exactly: one interface, no fan-out (unlike
/// IMedicineSourceProvider, which IS meant for multiple simultaneous implementations — eRx transmission
/// is a single active channel, not an aggregator), a (bool Success, string? Error) result rather than
/// throwing, and the implementation checks configuration and no-ops gracefully with a clear message.
/// See NotConfiguredPrescriptionTransmissionService, the only registered implementation for now.</summary>
public interface IPrescriptionTransmissionService
{
    Task<(bool Success, string? Error)> TransmitAsync(Prescription prescription, PrescriptionIssue issue);
}

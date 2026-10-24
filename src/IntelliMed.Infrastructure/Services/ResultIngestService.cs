using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Hl7;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Turns a raw HL7 result message into a <see cref="ResultReport"/> in the clinic's inbox.
///
/// Patient matching is deliberately conservative: a report is filed under a client only when exactly
/// one client in the clinic fits. Zero or several candidates leave it Unmatched for a person to
/// resolve, because a result filed under the wrong patient is worse than one that waits.
/// </summary>
public class ResultIngestService : IResultIngestService
{
    private readonly AppDbContext _context;
    private readonly Hl7ResultParser _parser = new();

    public ResultIngestService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IngestResultOutcomeDto> IngestAsync(string hl7Message, int clinicId)
    {
        ParsedResult parsed;
        try
        {
            parsed = _parser.Parse(hl7Message);
        }
        catch (Hl7ParseException ex)
        {
            throw new ArgumentException(ex.Message, nameof(hl7Message), ex);
        }

        var existing = await _context.ResultReports
            .Where(r => r.ClinicId == clinicId
                        && r.SenderName == parsed.SenderName
                        && r.MessageControlId == parsed.MessageControlId)
            .Select(r => new { r.Id, r.Status })
            .FirstOrDefaultAsync();
        if (existing != null)
            return new IngestResultOutcomeDto { ReportId = existing.Id, Status = existing.Status, Duplicate = true };

        var clientId = await MatchClientAsync(parsed, clinicId);
        var practitionerId = await FindOrderingPractitionerAsync(parsed);

        var report = new ResultReport
        {
            ClinicId = clinicId,
            ClientId = clientId,
            AssignedPractitionerId = practitionerId,
            Status = clientId.HasValue ? ResultStatus.Inbox : ResultStatus.Unmatched,
            SenderName = parsed.SenderName,
            MessageControlId = parsed.MessageControlId,
            PatientIdentifier = parsed.PatientIdentifier,
            PatientFamilyName = parsed.PatientFamilyName,
            PatientGivenName = parsed.PatientGivenName,
            PatientDateOfBirth = parsed.PatientDateOfBirth,
            PatientSex = parsed.PatientSex,
            PatientMedicareNumber = parsed.PatientMedicareNumber,
            OrderingProviderName = parsed.OrderingProviderName,
            OrderingProviderNumber = parsed.OrderingProviderNumber,
            PlacerOrderNumber = parsed.PlacerOrderNumber,
            FillerOrderNumber = parsed.FillerOrderNumber,
            ReportName = parsed.ReportName,
            Kind = parsed.Kind,
            CollectedAt = parsed.CollectedAt,
            ReportedAt = parsed.ReportedAt,
            HasAbnormal = parsed.Items.Any(i => i.IsAbnormal),
            RawMessage = parsed.RawMessage,
            ReceivedAt = DateTime.UtcNow,
            Items = parsed.Items,
            Attachments = parsed.Attachments
        };

        _context.ResultReports.Add(report);
        await _context.SaveChangesAsync();

        return new IngestResultOutcomeDto { ReportId = report.Id, Status = report.Status };
    }

    /// <summary>The single client this report belongs to, or null when there is none or more than one.</summary>
    internal async Task<int?> MatchClientAsync(ParsedResult parsed, int clinicId)
    {
        // 1. The lab's own patient number, remembered from an earlier manual match.
        if (!string.IsNullOrWhiteSpace(parsed.PatientIdentifier))
        {
            var remembered = await _context.ClientLabIdentifiers
                .Where(l => l.SenderName == parsed.SenderName
                            && l.Identifier == parsed.PatientIdentifier
                            && l.Client!.ClinicId == clinicId)
                .Select(l => l.ClientId)
                .Distinct()
                .Take(2)
                .ToListAsync();
            if (remembered.Count == 1) return remembered[0];
            if (remembered.Count > 1) return null;
        }

        // Everything below needs a date of birth: a name or Medicare number alone is not safe.
        if (!parsed.PatientDateOfBirth.HasValue) return null;
        var dob = parsed.PatientDateOfBirth.Value.Date;
        var dobEnd = dob.AddDays(1);

        var sameDob = await _context.Clients
            .Where(c => c.ClinicId == clinicId
                        && c.Type == ClientTypeEnum.Person
                        && c.DateOfBirth >= dob
                        && c.DateOfBirth < dobEnd)
            .Select(c => new { c.Id, c.FirstName, c.LastName, c.MedicareNumber })
            .ToListAsync();
        if (sameDob.Count == 0) return null;

        // 2. Medicare number (first 9 digits identify the card, the 10th is the check digit) + DOB.
        var medicare = MedicareKey(parsed.PatientMedicareNumber);
        if (medicare != null)
        {
            var byMedicare = sameDob.Where(c => MedicareKey(c.MedicareNumber) == medicare).Select(c => c.Id).Distinct().ToList();
            if (byMedicare.Count == 1) return byMedicare[0];
            if (byMedicare.Count > 1) return null;
        }

        // 3. Exact family + given name + DOB.
        if (!string.IsNullOrWhiteSpace(parsed.PatientFamilyName) && !string.IsNullOrWhiteSpace(parsed.PatientGivenName))
        {
            var byName = sameDob
                .Where(c => c.LastName.Trim().Equals(parsed.PatientFamilyName.Trim(), StringComparison.OrdinalIgnoreCase)
                            && c.FirstName.Trim().Equals(parsed.PatientGivenName.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Id)
                .ToList();
            if (byName.Count == 1) return byName[0];
        }

        return null;
    }

    /// <summary>
    /// Route to the ordering doctor when they can be identified with confidence: by Medicare provider
    /// number, or by exact family + given name. Anything weaker leaves it in the clinic-wide inbox —
    /// a wrong guess would hide the result from the doctor who ordered it.
    /// </summary>
    internal async Task<int?> FindOrderingPractitionerAsync(ParsedResult parsed)
    {
        if (!string.IsNullOrWhiteSpace(parsed.OrderingProviderNumber))
        {
            var number = parsed.OrderingProviderNumber.Trim();
            var byNumber = await _context.Practitioners
                .Where(p => p.IsActive && p.ProviderNumber == number)
                .Select(p => p.Id)
                .Take(2)
                .ToListAsync();
            if (byNumber.Count == 1) return byNumber[0];
        }

        if (string.IsNullOrWhiteSpace(parsed.OrderingProviderName)) return null;

        var name = parsed.OrderingProviderName.Trim();
        var candidates = await _context.Practitioners
            .Where(p => p.IsActive)
            .Select(p => new { p.Id, p.FirstName, p.LastName })
            .ToListAsync();

        var byName = candidates
            .Where(p => p.LastName.Trim().Length > 0
                        && p.FirstName.Trim().Length > 0
                        && name.EndsWith(p.LastName.Trim(), StringComparison.OrdinalIgnoreCase)
                        && name.Contains(p.FirstName.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Id)
            .ToList();

        return byName.Count == 1 ? byName[0] : null;
    }

    private static string? MedicareKey(string? number)
    {
        if (string.IsNullOrWhiteSpace(number)) return null;
        var digits = new string(number.Where(char.IsDigit).ToArray());
        return digits.Length >= 9 ? digits[..9] : null;
    }
}

using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

public interface IResultIngestService
{
    /// <summary>
    /// Parse an HL7 v2 ORU^R01 message, try to match it to a patient and file it in the clinic's
    /// results inbox. Throws <see cref="ArgumentException"/> when the message cannot be read or is not
    /// a result message. Re-sending a message already received returns <c>Duplicate = true</c>.
    /// </summary>
    Task<IngestResultOutcomeDto> IngestAsync(string hl7Message, int clinicId);
}

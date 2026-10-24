namespace IntelliMed.Core.Entities;

/// <summary>
/// One pathology or imaging report received from a lab — normally parsed from an HL7 v2 ORU^R01
/// message. The inbox is built from these. A report is filed under a patient only when the match is
/// unambiguous; anything else sits as <see cref="ResultStatus.Unmatched"/> until staff pick the
/// patient (never auto-filed on a guess — a result under the wrong patient is a safety problem).
///
/// Nothing leaves the inbox silently: only an explicit acknowledgement archives a report, and that
/// records who and when.
/// </summary>
public class ResultReport
{
    public int Id { get; set; }

    /// <summary>The clinic the report was received for — taken from the ingest key, or the uploader's
    /// selected clinic.</summary>
    public int ClinicId { get; set; }

    /// <summary>Null while <see cref="Status"/> is Unmatched.</summary>
    public int? ClientId { get; set; }

    /// <summary>The doctor whose inbox the report is in. Null = clinic-wide inbox (the ordering
    /// provider could not be identified).</summary>
    public int? AssignedPractitionerId { get; set; }

    public ResultStatus Status { get; set; } = ResultStatus.Unmatched;

    // ---- As received (kept verbatim so a manual match can be checked against what the lab sent) ----

    /// <summary>Sending lab / facility (MSH-4, falling back to MSH-3).</summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>MSH-10 — with <see cref="SenderName"/> the idempotency key, so a re-sent message
    /// does not create a second report.</summary>
    public string MessageControlId { get; set; } = string.Empty;

    /// <summary>The lab's own patient identifier (PID-3).</summary>
    public string? PatientIdentifier { get; set; }
    public string? PatientFamilyName { get; set; }
    public string? PatientGivenName { get; set; }
    public DateTime? PatientDateOfBirth { get; set; }
    public string? PatientSex { get; set; }
    public string? PatientMedicareNumber { get; set; }

    public string? OrderingProviderName { get; set; }
    public string? OrderingProviderNumber { get; set; }

    /// <summary>Our order number echoed back by the lab (OBR-2) — what clears an outstanding request.</summary>
    public string? PlacerOrderNumber { get; set; }

    /// <summary>The lab's accession / order number (OBR-3).</summary>
    public string? FillerOrderNumber { get; set; }

    /// <summary>OBR-4 — "Full Blood Count", "Chest X-ray".</summary>
    public string? ReportName { get; set; }

    public ResultReportKind Kind { get; set; } = ResultReportKind.Pathology;

    public DateTime? CollectedAt { get; set; }
    public DateTime? ReportedAt { get; set; }

    /// <summary>True when any item carries an abnormal flag. Denormalised so the inbox can sort and
    /// badge without loading items.</summary>
    public bool HasAbnormal { get; set; }

    /// <summary>The message as received, kept for audit.</summary>
    public string RawMessage { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    // ---- Review trail ----

    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }

    /// <summary>Who matched an Unmatched report to a patient, and when.</summary>
    public string? MatchedByUserId { get; set; }
    public DateTime? MatchedAt { get; set; }

    public string? HoldNote { get; set; }

    public Client? Client { get; set; }
    public Practitioner? AssignedPractitioner { get; set; }
    public ICollection<ResultItem> Items { get; set; } = new List<ResultItem>();
    public ICollection<ResultAttachment> Attachments { get; set; } = new List<ResultAttachment>();
}

public enum ResultStatus
{
    /// <summary>Received but not filed under a patient — needs a person to match it.</summary>
    Unmatched = 0,
    /// <summary>Filed under a patient, waiting for a doctor to acknowledge.</summary>
    Inbox = 1,
    /// <summary>Parked by a doctor to review later. Still unacknowledged.</summary>
    Held = 2,
    /// <summary>Acknowledged.</summary>
    Archived = 3
}

public enum ResultReportKind
{
    Pathology = 0,
    Imaging = 1,
    Other = 2
}

/// <summary>One test line within a report (an OBX segment).</summary>
public class ResultItem
{
    public int Id { get; set; }
    public int ResultReportId { get; set; }
    public int Sequence { get; set; }

    /// <summary>OBX-3 identifier, e.g. "HB".</summary>
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Units { get; set; }
    public string? ReferenceRange { get; set; }

    /// <summary>OBX-8 as sent: H, L, HH, LL, A, N...</summary>
    public string? AbnormalFlag { get; set; }
    public bool IsAbnormal { get; set; }

    /// <summary>OBX-11 result status (F final, P preliminary, C corrected).</summary>
    public string? ResultStatus { get; set; }

    /// <summary>NTE comment lines that followed this OBX, joined.</summary>
    public string? Comment { get; set; }

    public ResultReport? ResultReport { get; set; }
}

/// <summary>A document the lab embedded in the message (an ED/RP OBX) — usually the PDF report.</summary>
public class ResultAttachment
{
    public int Id { get; set; }
    public int ResultReportId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public ResultReport? ResultReport { get; set; }
}

/// <summary>Remembers a lab's own patient number for one of our clients, so the lab's next report
/// matches automatically once staff have matched the first.</summary>
public class ClientLabIdentifier
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Client? Client { get; set; }
}

/// <summary>
/// A credential an external system (a vendor feed, a clinic-side relay) uses to POST results to
/// /api/results/ingest. Scoped to one clinic. Only a SHA-256 hash of the key is stored — the key is
/// shown once at creation and cannot be recovered.
/// </summary>
public class ResultIngestKey
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public string Label { get; set; } = string.Empty;

    /// <summary>First characters of the key, shown in the list so an admin can tell keys apart.</summary>
    public string KeyPrefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }

    public Clinic? Clinic { get; set; }
}

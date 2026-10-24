using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class ResultSummaryDto
{
    public int Id { get; set; }
    public ResultStatus Status { get; set; }
    public ResultReportKind Kind { get; set; }
    public int? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string PatientNameAsReceived { get; set; } = string.Empty;
    public DateTime? PatientDateOfBirth { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? ReportName { get; set; }
    public DateTime? CollectedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public bool HasAbnormal { get; set; }
    public int? AssignedPractitionerId { get; set; }
    public string? AssignedPractitionerName { get; set; }
    public string? OrderingProviderName { get; set; }
}

public class ResultItemDto
{
    public int Sequence { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Units { get; set; }
    public string? ReferenceRange { get; set; }
    public string? AbnormalFlag { get; set; }
    public bool IsAbnormal { get; set; }
    public string? ResultStatus { get; set; }
    public string? Comment { get; set; }
}

public class ResultAttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}

public class ResultDetailDto : ResultSummaryDto
{
    public string MessageControlId { get; set; } = string.Empty;
    public string? PatientIdentifier { get; set; }
    public string? PatientSex { get; set; }
    public string? PatientMedicareNumber { get; set; }
    public string? OrderingProviderNumber { get; set; }
    public string? PlacerOrderNumber { get; set; }
    public string? FillerOrderNumber { get; set; }
    public DateTime? ReportedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReviewComment { get; set; }
    public string? HoldNote { get; set; }
    public List<ResultItemDto> Items { get; set; } = new();
    public List<ResultAttachmentDto> Attachments { get; set; } = new();
}

public class ResultSearchDto
{
    public int? ClinicId { get; set; }
    public ResultStatus? Status { get; set; }
    public int? ClientId { get; set; }

    /// <summary>When set, only reports in this practitioner's inbox plus clinic-wide (unassigned) ones.</summary>
    public int? PractitionerId { get; set; }

    public string? Query { get; set; }
    public bool AbnormalOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

/// <summary>How many reports are waiting, for the sidebar/tab badges.</summary>
public class ResultCountsDto
{
    public int Inbox { get; set; }
    public int Unmatched { get; set; }
    public int Held { get; set; }
    public int InboxAbnormal { get; set; }
}

public class MatchResultDto
{
    public int ClientId { get; set; }

    /// <summary>Remember the lab's patient identifier for this client so the lab's next report
    /// matches by itself.</summary>
    public bool RememberLabIdentifier { get; set; } = true;
}

public class AcknowledgeResultDto
{
    public string? Comment { get; set; }
}

public class HoldResultDto
{
    public string? Note { get; set; }
}

public class IngestResultOutcomeDto
{
    public int ReportId { get; set; }
    public ResultStatus Status { get; set; }

    /// <summary>True when the message had already been received (same sender + control id) and was ignored.</summary>
    public bool Duplicate { get; set; }
}

public class ResultIngestKeyDto
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class CreateResultIngestKeyDto
{
    public string Label { get; set; } = string.Empty;
}

/// <summary>Returned once, at creation. <see cref="Key"/> is never retrievable again.</summary>
public class CreatedResultIngestKeyDto : ResultIngestKeyDto
{
    public string Key { get; set; } = string.Empty;
}

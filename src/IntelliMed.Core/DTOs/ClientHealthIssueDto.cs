using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class ClientHealthIssueDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? CodingSystem { get; set; }
    public BodySide? Side { get; set; }
    public int? OnsetYear { get; set; }
    public int? OnsetMonth { get; set; }
    public bool IsActive { get; set; }
    public bool IsSummary { get; set; }
    public bool IsConfidential { get; set; }
    public string? Note { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Onset rendered for display ("Mar 2015", "2015", or empty) — computed here rather than
    /// in each caller, since year-without-month is the common case and formatting it is fiddly.</summary>
    public string OnsetDisplay =>
        OnsetYear == null
            ? string.Empty
            : OnsetMonth is >= 1 and <= 12
                ? $"{System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(OnsetMonth.Value)} {OnsetYear}"
                : OnsetYear.Value.ToString();
}

public class CreateClientHealthIssueDto
{
    public int ClientId { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? CodingSystem { get; set; }
    public BodySide? Side { get; set; }
    public int? OnsetYear { get; set; }
    public int? OnsetMonth { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSummary { get; set; }
    public bool IsConfidential { get; set; }
    public string? Note { get; set; }

    /// <summary>Which visit this issue was recorded during, when known. Drives the "log it into the
    /// open progress note" behaviour in ClientHealthIssuesController.Create — same optional-link
    /// pattern Prescription.AppointmentId uses.</summary>
    public int? AppointmentId { get; set; }
}

public class UpdateClientHealthIssueDto
{
    public string Condition { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? CodingSystem { get; set; }
    public BodySide? Side { get; set; }
    public int? OnsetYear { get; set; }
    public int? OnsetMonth { get; set; }
    public bool IsActive { get; set; }
    public bool IsSummary { get; set; }
    public bool IsConfidential { get; set; }
    public string? Note { get; set; }
    public bool IsArchived { get; set; }
}

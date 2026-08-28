namespace IntelliMed.Core.Entities;

/// <summary>
/// One entry on a client's problem list — a condition they have or have had. Ported from legacy
/// Medinet's `History` entity (ClinNet/Controls/PastHistoryCtrl.cs, ClinNet/Forms/frmAddHistory.cs),
/// whose own domain term for these rows is "health issue".
///
/// This is the record of a patient's ongoing clinical *state*, as opposed to ProgressNote which
/// records an individual *encounter*, and Episode which is an organisational label grouping notes.
/// </summary>
public class ClientHealthIssue
{
    public int Id { get; set; }
    public int ClientId { get; set; }

    /// <summary>The problem itself, e.g. "Type 2 diabetes". Free text — see Code.</summary>
    public string Condition { get; set; } = string.Empty;

    /// <summary>Optional coded form of Condition. Medinet codes against ICPC-2 PLUS, which is
    /// licensed and unavailable here (same constraint as MIMS), so nothing populates these yet and
    /// there is no code picker. They exist so a coded vocabulary can be layered on later without a
    /// schema change — and because drug-to-disease interaction checking, if it ever arrives, needs
    /// coded conditions rather than free text.</summary>
    public string? Code { get; set; }
    public string? CodingSystem { get; set; }

    public BodySide? Side { get; set; }

    /// <summary>Onset as year (+ optional month) rather than a full date, copied deliberately from
    /// Medinet's YEAR_TRANSPIRED/MONTH_TRANSPIRED. Real onset is usually "sometime in 2015"; a
    /// DateTime would force a fabricated day.</summary>
    public int? OnsetYear { get; set; }
    public int? OnsetMonth { get; set; }

    /// <summary>Clinical state: is this problem current? Distinct from IsArchived — an inactive
    /// issue is still a true part of the patient's history and stays on the list. Medinet's ACTIVE.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Pin to the patient summary — drives the Summary filter on the Health Issues tab and
    /// the compact list shown on the progress-note editor. Medinet's SUMMARY.</summary>
    public bool IsSummary { get; set; }

    /// <summary>Restricted-visibility marker carried over from Medinet's CONFIDENTIAL. Stored and
    /// displayed as a flag; it does not currently filter the row out for any role.</summary>
    public bool IsConfidential { get; set; }

    public string? Note { get; set; }

    /// <summary>Record-level soft delete for entered-in-error rows — deliberately NOT the same thing
    /// as IsActive. Same convention as ClientAllergy.</summary>
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
}

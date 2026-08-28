namespace IntelliMed.Core.Entities;

/// <summary>A named grouping of ProgressNotes around one ongoing clinical issue (e.g. "Right knee
/// injury — Mar 2026"). Optional — most single-visit notes never set ProgressNote.EpisodeId. An
/// organizational label, not itself a legal document, so it's Archived/Resolved like ClientReferral
/// rather than given Prescription's harder "no archive concept, Status only" treatment.</summary>
public class Episode
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EpisodeStatus Status { get; set; } = EpisodeStatus.Active;
    public DateTime OpenedDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? ClosedDate { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public ICollection<ProgressNote> ProgressNotes { get; set; } = new List<ProgressNote>();
}

namespace IntelliMed.Core.Entities;

/// <summary>Append-only, same shape as PrescriptionIssue/FeeScheduleItemPriceHistory — created only via
/// ProgressNoteRepository, never updated or deleted. Two uses: (1) a manual correction typed by a
/// practitioner once the parent ProgressNote is Finalized — the only way to add information after
/// finalization; (2) a System-sourced entry auto-appended by another module (Prescribing today) while
/// this note is open for the same encounter, found via ProgressNoteId's Appointment link rather than
/// any session-based coincidence. SourceReferenceType/SourceReferenceId let a System entry deep-link
/// back to the record that triggered it (e.g. "Prescription", 42).</summary>
public class ProgressNoteAddendum
{
    public int Id { get; set; }
    public int ProgressNoteId { get; set; }
    public string Content { get; set; } = string.Empty;
    public ProgressNoteAddendumSource Source { get; set; } = ProgressNoteAddendumSource.Practitioner;
    public string? SourceReferenceType { get; set; }
    public int? SourceReferenceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByPractitionerId { get; set; }

    public ProgressNote? ProgressNote { get; set; }
    public Practitioner? CreatedByPractitioner { get; set; }
}

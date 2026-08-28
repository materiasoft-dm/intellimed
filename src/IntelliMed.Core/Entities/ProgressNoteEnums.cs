namespace IntelliMed.Core.Entities;

public enum ProgressNoteStatus
{
    Draft,
    Finalized
}

/// <summary>Distinguishes a manual correction typed by a practitioner (only allowed once the parent
/// note is Finalized) from a system-generated line auto-appended by another module — e.g. Prescribing
/// — while this note is open for the same encounter. System entries can land whether the parent note
/// is still Draft or already Finalized.</summary>
public enum ProgressNoteAddendumSource
{
    Practitioner,
    System
}

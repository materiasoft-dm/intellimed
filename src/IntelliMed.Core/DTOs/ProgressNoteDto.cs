using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class ProgressNoteDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int? AppointmentId { get; set; }
    public int PractitionerId { get; set; }
    public string PractitionerName { get; set; } = string.Empty;
    public int? EpisodeId { get; set; }
    public string? EpisodeTitle { get; set; }
    public DateTime EncounterDate { get; set; }
    public string? ReasonForVisit { get; set; }
    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string? Assessment { get; set; }
    public string? Plan { get; set; }
    public DateTime? ReviewDate { get; set; }
    public ProgressNoteStatus Status { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<ProgressNoteAddendumDto> Addenda { get; set; } = new();
}

public class CreateProgressNoteDto
{
    public int ClientId { get; set; }
    public int? AppointmentId { get; set; }
    public int PractitionerId { get; set; }
    public int? EpisodeId { get; set; }
    public DateTime EncounterDate { get; set; }
    public string? ReasonForVisit { get; set; }
    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string? Assessment { get; set; }
    public string? Plan { get; set; }
    public DateTime? ReviewDate { get; set; }
}

/// <summary>Only the fields the repository will actually accept while the note is still Draft — see
/// ProgressNoteRepository.UpdateAsync for which of these are silently ignored once Status == Finalized.
/// AppointmentId is deliberately absent — set once at creation, never re-pointed, same convention as
/// Prescription.AppointmentId not appearing in UpdatePrescriptionDto.</summary>
public class UpdateProgressNoteDto
{
    public int PractitionerId { get; set; }
    public int? EpisodeId { get; set; }
    public DateTime EncounterDate { get; set; }
    public string? ReasonForVisit { get; set; }
    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string? Assessment { get; set; }
    public string? Plan { get; set; }
    public DateTime? ReviewDate { get; set; }
}

/// <summary>Offers to also mark the linked Appointment Completed — a nudge, not forced, since front-desk
/// checkout sometimes happens separately from the clinical documentation being finished.</summary>
public class FinalizeProgressNoteDto
{
    public bool MarkAppointmentCompleted { get; set; } = true;
}

public class ProgressNoteAddendumDto
{
    public int Id { get; set; }
    public int ProgressNoteId { get; set; }
    public string Content { get; set; } = string.Empty;
    public ProgressNoteAddendumSource Source { get; set; }
    public string? SourceReferenceType { get; set; }
    public int? SourceReferenceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByPractitionerId { get; set; }
    public string CreatedByPractitionerName { get; set; } = string.Empty;
}

/// <summary>Input for a manual addendum — only accepted once the parent note is Finalized (see
/// ProgressNoteRepository.AddAddendumAsync). System-sourced entries are never created through this DTO;
/// they come from ProgressNoteRepository.AppendSystemEntryAsync, called by other modules.</summary>
public class AddProgressNoteAddendumDto
{
    public string Content { get; set; } = string.Empty;
    public int CreatedByPractitionerId { get; set; }
}

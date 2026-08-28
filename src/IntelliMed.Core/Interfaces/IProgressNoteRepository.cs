using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IProgressNoteRepository : IRepository<ProgressNote>
{
    new Task<ProgressNoteDto?> GetByIdAsync(int id);
    Task<IEnumerable<ProgressNoteDto>> GetByClientIdAsync(int clientId);
    Task<int> CreateAsync(CreateProgressNoteDto dto);
    Task UpdateAsync(int id, UpdateProgressNoteDto dto);
    Task FinalizeAsync(int id, FinalizeProgressNoteDto dto);

    /// <summary>Manual correction, only accepted once the parent note is Finalized — the only way to
    /// add information after finalization.</summary>
    Task<int> AddAddendumAsync(int progressNoteId, AddProgressNoteAddendumDto dto);

    /// <summary>Called by other modules (Prescribing today) when a clinical action happens during an
    /// encounter — looks up the ProgressNote (Draft or Finalized) linked to this AppointmentId and
    /// appends a System-sourced ProgressNoteAddendum. No-ops if no note is linked to that appointment,
    /// matching the "only if a progress note is open" behaviour this mirrors from legacy Medinet.</summary>
    Task AppendSystemEntryAsync(int appointmentId, string content, string sourceReferenceType, int sourceReferenceId, int practitionerId);
}

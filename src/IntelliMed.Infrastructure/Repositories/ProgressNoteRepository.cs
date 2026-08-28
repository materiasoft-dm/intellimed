using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ProgressNoteRepository : Repository<ProgressNote>, IProgressNoteRepository
{
    public ProgressNoteRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<ProgressNoteDto?> GetByIdAsync(int id)
    {
        var note = await _dbSet
            .Include(n => n.Practitioner)
            .Include(n => n.Episode)
            .Include(n => n.Addenda).ThenInclude(a => a.CreatedByPractitioner)
            .FirstOrDefaultAsync(n => n.Id == id);
        return note == null ? null : EntityMapper.ToDto(note);
    }

    public async Task<IEnumerable<ProgressNoteDto>> GetByClientIdAsync(int clientId)
    {
        var notes = await _dbSet
            .Include(n => n.Practitioner)
            .Include(n => n.Episode)
            .Include(n => n.Addenda).ThenInclude(a => a.CreatedByPractitioner)
            .Where(n => n.ClientId == clientId)
            .OrderByDescending(n => n.EncounterDate)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();
        return notes.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateProgressNoteDto dto)
    {
        var client = await _context.Clients.FindAsync(dto.ClientId);
        if (client != null && client.Deceased)
            throw new InvalidOperationException($"Client {dto.ClientId} is recorded as deceased — a new progress note cannot be created for them.");

        var encounterDate = dto.EncounterDate;

        // Derive from the linked Appointment when one exists — new logic, no existing helper does this.
        if (dto.AppointmentId.HasValue)
        {
            var appointment = await _context.Appointments.FindAsync(dto.AppointmentId.Value);
            if (appointment != null)
                encounterDate = appointment.AppointmentDate;
        }

        var note = new ProgressNote
        {
            ClientId = dto.ClientId,
            AppointmentId = dto.AppointmentId,
            PractitionerId = dto.PractitionerId,
            EpisodeId = dto.EpisodeId,
            EncounterDate = encounterDate,
            ReasonForVisit = dto.ReasonForVisit,
            Subjective = dto.Subjective,
            Objective = dto.Objective,
            Assessment = dto.Assessment,
            Plan = dto.Plan,
            ReviewDate = dto.ReviewDate,
            Status = ProgressNoteStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        await _dbSet.AddAsync(note);
        await _context.SaveChangesAsync();
        return note.Id;
    }

    public async Task UpdateAsync(int id, UpdateProgressNoteDto dto)
    {
        var note = await _dbSet.FirstOrDefaultAsync(n => n.Id == id);
        if (note == null)
            throw new InvalidOperationException($"ProgressNote with ID {id} not found");

        // Locked once Finalized — a signed note is a fixed clinical event (see ProgressNote.cs's class
        // doc comment). Server-side backstop for a direct API call bypassing the UI's own read-only
        // rendering once Status == Finalized; corrections go through AddAddendumAsync instead.
        if (note.Status != ProgressNoteStatus.Draft)
            throw new InvalidOperationException($"ProgressNote {id} is {note.Status} and can no longer be edited directly — add an addendum instead.");

        note.PractitionerId = dto.PractitionerId;
        note.EpisodeId = dto.EpisodeId;
        note.EncounterDate = dto.EncounterDate;
        note.ReasonForVisit = dto.ReasonForVisit;
        note.Subjective = dto.Subjective;
        note.Objective = dto.Objective;
        note.Assessment = dto.Assessment;
        note.Plan = dto.Plan;
        note.ReviewDate = dto.ReviewDate;
        note.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task FinalizeAsync(int id, FinalizeProgressNoteDto dto)
    {
        var note = await _dbSet.FirstOrDefaultAsync(n => n.Id == id);
        if (note == null)
            throw new InvalidOperationException($"ProgressNote with ID {id} not found");
        if (note.Status == ProgressNoteStatus.Finalized)
            throw new InvalidOperationException($"ProgressNote {id} is already Finalized.");

        note.Status = ProgressNoteStatus.Finalized;
        note.FinalizedAt = DateTime.UtcNow;
        note.UpdatedAt = DateTime.UtcNow;

        // A nudge, not forced — front-desk checkout sometimes happens separately from the clinical
        // documentation being finished.
        if (dto.MarkAppointmentCompleted && note.AppointmentId.HasValue)
        {
            var appointment = await _context.Appointments.FindAsync(note.AppointmentId.Value);
            if (appointment != null
                && appointment.Status != AppointmentStatus.Completed
                && appointment.Status != AppointmentStatus.Cancelled)
            {
                appointment.Status = AppointmentStatus.Completed;
                appointment.CompletedAt = DateTime.UtcNow;
                appointment.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    public override async Task DeleteAsync(int id)
    {
        var note = await _dbSet.FirstOrDefaultAsync(n => n.Id == id);
        if (note == null)
            throw new InvalidOperationException($"ProgressNote with ID {id} not found");

        // Only a not-yet-real (never finalized) draft can be discarded — matches Prescription having no
        // delete route at all once a document is real.
        if (note.Status != ProgressNoteStatus.Draft)
            throw new InvalidOperationException($"ProgressNote {id} is {note.Status} and can no longer be deleted — it's part of the clinical record.");

        _dbSet.Remove(note);
        await _context.SaveChangesAsync();
    }

    public async Task<int> AddAddendumAsync(int progressNoteId, AddProgressNoteAddendumDto dto)
    {
        var note = await _dbSet.FirstOrDefaultAsync(n => n.Id == progressNoteId);
        if (note == null)
            throw new InvalidOperationException($"ProgressNote with ID {progressNoteId} not found");
        if (note.Status != ProgressNoteStatus.Finalized)
            throw new InvalidOperationException($"ProgressNote {progressNoteId} is still Draft — edit it directly instead of adding an addendum.");

        var addendum = new ProgressNoteAddendum
        {
            ProgressNoteId = progressNoteId,
            Content = dto.Content,
            Source = ProgressNoteAddendumSource.Practitioner,
            CreatedAt = DateTime.UtcNow,
            CreatedByPractitionerId = dto.CreatedByPractitionerId
        };

        await _context.ProgressNoteAddenda.AddAsync(addendum);
        await _context.SaveChangesAsync();
        return addendum.Id;
    }

    public async Task AppendSystemEntryAsync(int appointmentId, string content, string sourceReferenceType, int sourceReferenceId, int practitionerId)
    {
        // Prefer whichever note is still open (Draft) for this encounter, else fall back to the most
        // recently created one — no-ops entirely if none exists, matching the "only if a progress note
        // is open" behaviour this mirrors from legacy Medinet.
        var note = await _dbSet
            .Where(n => n.AppointmentId == appointmentId)
            .OrderBy(n => n.Status == ProgressNoteStatus.Draft ? 0 : 1)
            .ThenByDescending(n => n.CreatedAt)
            .FirstOrDefaultAsync();
        if (note == null)
            return;

        var addendum = new ProgressNoteAddendum
        {
            ProgressNoteId = note.Id,
            Content = content,
            Source = ProgressNoteAddendumSource.System,
            SourceReferenceType = sourceReferenceType,
            SourceReferenceId = sourceReferenceId,
            CreatedAt = DateTime.UtcNow,
            CreatedByPractitionerId = practitionerId
        };

        await _context.ProgressNoteAddenda.AddAsync(addendum);
        await _context.SaveChangesAsync();
    }
}

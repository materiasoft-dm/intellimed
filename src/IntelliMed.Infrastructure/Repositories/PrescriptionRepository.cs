using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class PrescriptionRepository : Repository<Prescription>, IPrescriptionRepository
{
    public PrescriptionRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<PrescriptionDto?> GetByIdAsync(int id)
    {
        var prescription = await _dbSet
            .Include(p => p.PrescribedByPractitioner)
            .Include(p => p.Episode)
            .Include(p => p.Issues)
            .FirstOrDefaultAsync(p => p.Id == id);
        return prescription == null ? null : EntityMapper.ToDto(prescription);
    }

    public async Task<IEnumerable<PrescriptionDto>> GetByClientIdAsync(int clientId, bool includeInactive = false)
    {
        var query = _dbSet
            .Include(p => p.PrescribedByPractitioner)
            .Include(p => p.Episode)
            .Include(p => p.Issues)
            .Where(p => p.ClientId == clientId);
        if (!includeInactive)
            query = query.Where(p => p.Status == PrescriptionStatus.Active);

        var prescriptions = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return prescriptions.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreatePrescriptionDto dto)
    {
        var now = DateTime.UtcNow;
        var prescription = new Prescription
        {
            ClientId = dto.ClientId,
            MedicineId = dto.MedicineId,
            MedicineName = dto.MedicineName,
            Dose = dto.Dose,
            Frequency = dto.Frequency,
            Instruction = dto.Instruction,
            Route = dto.Route,
            Purpose = dto.Purpose,
            IsLimitedDuration = dto.IsLimitedDuration,
            Quantity = dto.Quantity,
            DurationDays = dto.DurationDays,
            Repeats = dto.Repeats,
            RepeatIntervalDays = dto.RepeatIntervalDays,
            AuthorityApprovalNumber = dto.AuthorityApprovalNumber,
            StateAuthorityCode = dto.StateAuthorityCode,
            PrescribingNotes = dto.PrescribingNotes,
            NotesForPharmacy = dto.NotesForPharmacy,
            Reason = dto.Reason,
            EpisodeId = dto.EpisodeId,
            AppointmentId = dto.AppointmentId,
            PrescribedByPractitionerId = dto.PrescribedByPractitionerId,
            SafetyOverrideAcknowledged = dto.SafetyOverrideAcknowledged,
            SafetyOverrideNote = dto.SafetyOverrideNote,
            Status = PrescriptionStatus.Active,
            StartDate = dto.StartDate ?? now,
            CreatedAt = now
        };

        // Snapshot from the catalog at creation time — frozen from here on, see Prescription.cs's
        // class doc comment for why this isn't re-synced on later Medicine edits.
        if (dto.MedicineId.HasValue)
        {
            var medicine = await _context.Medicines.FindAsync(dto.MedicineId.Value);
            if (medicine != null)
            {
                prescription.IsPbsListed = medicine.IsPbsListed;
                prescription.IsRpbsListed = medicine.IsRpbsListed;
                prescription.RestrictionLevel = medicine.RestrictionLevel;
                prescription.Schedule = medicine.Schedule;
            }
        }

        prescription.ScriptNumber = await GenerateScriptNumberAsync();

        await _dbSet.AddAsync(prescription);
        await _context.SaveChangesAsync();
        return prescription.Id;
    }

    public async Task UpdateAsync(int id, UpdatePrescriptionDto dto)
    {
        var prescription = await _dbSet.Include(p => p.Issues).FirstOrDefaultAsync(p => p.Id == id);
        if (prescription == null)
            throw new InvalidOperationException($"Prescription with ID {id} not found");

        // Always editable, issued or not.
        prescription.PrescribingNotes = dto.PrescribingNotes;
        prescription.NotesForPharmacy = dto.NotesForPharmacy;
        prescription.Reason = dto.Reason;
        prescription.EpisodeId = dto.EpisodeId;

        // Locked once at least one issue exists — a printed/issued script is a fixed clinical event
        // (see Prescription.cs's class doc comment). The UI disables these fields once IsIssued is
        // true; this is the server-side backstop for a direct API call bypassing that, matching this
        // codebase's general discipline of not trusting the client alone for state-machine rules.
        if (prescription.Issues.Count == 0)
        {
            prescription.Dose = dto.Dose;
            prescription.Frequency = dto.Frequency;
            prescription.Instruction = dto.Instruction;
            prescription.Route = dto.Route;
            prescription.Purpose = dto.Purpose;
            prescription.IsLimitedDuration = dto.IsLimitedDuration;
            prescription.Quantity = dto.Quantity;
            prescription.DurationDays = dto.DurationDays;
            prescription.Repeats = dto.Repeats;
            prescription.RepeatIntervalDays = dto.RepeatIntervalDays;
            prescription.AuthorityApprovalNumber = dto.AuthorityApprovalNumber;
            prescription.StateAuthorityCode = dto.StateAuthorityCode;
        }

        prescription.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task CeaseAsync(int id)
    {
        var prescription = await _dbSet.FindAsync(id);
        if (prescription == null)
            throw new InvalidOperationException($"Prescription with ID {id} not found");

        prescription.Status = PrescriptionStatus.Ceased;
        prescription.CeasedDate = DateTime.UtcNow;
        prescription.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task CancelAsync(int id)
    {
        var prescription = await _dbSet.FindAsync(id);
        if (prescription == null)
            throw new InvalidOperationException($"Prescription with ID {id} not found");

        prescription.Status = PrescriptionStatus.Cancelled;
        prescription.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<List<ExistingMedicationIngredient>> GetActiveIngredientsForClientAsync(int clientId)
    {
        var activePrescriptions = await _dbSet
            .Where(p => p.ClientId == clientId && p.Status == PrescriptionStatus.Active && p.MedicineId != null)
            .Select(p => new { MedicineId = p.MedicineId!.Value, p.MedicineName })
            .ToListAsync();

        var result = new List<ExistingMedicationIngredient>();
        foreach (var group in activePrescriptions.GroupBy(p => p.MedicineId))
        {
            var ingredientIds = await _context.MedicineIngredients
                .Where(mi => mi.MedicineId == group.Key)
                .Select(mi => mi.ActiveIngredientId)
                .ToListAsync();

            foreach (var prescriptionInGroup in group)
            {
                result.AddRange(ingredientIds.Select(ingredientId =>
                    new ExistingMedicationIngredient(ingredientId, prescriptionInGroup.MedicineName, group.Key)));
            }
        }
        return result;
    }

    // Direct copy of InvoiceRepository.GenerateInvoiceNumberAsync's year-prefixed max+1 pattern — same
    // accepted non-concurrency-safe risk level as that established precedent.
    private async Task<string> GenerateScriptNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"RX-{year}-";

        var lastPrescription = await _dbSet
            .Where(p => p.ScriptNumber.StartsWith(prefix))
            .OrderByDescending(p => p.ScriptNumber)
            .FirstOrDefaultAsync();

        var nextNumber = 1;
        if (lastPrescription != null)
        {
            var lastNumberStr = lastPrescription.ScriptNumber.Replace(prefix, "");
            if (int.TryParse(lastNumberStr, out var lastNumber))
                nextNumber = lastNumber + 1;
        }
        return $"{prefix}{nextNumber:D5}";
    }
}

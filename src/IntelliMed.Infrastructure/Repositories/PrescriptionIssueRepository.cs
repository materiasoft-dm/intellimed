using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class PrescriptionIssueRepository : Repository<PrescriptionIssue>, IPrescriptionIssueRepository
{
    public PrescriptionIssueRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<PrescriptionIssueDto?> GetByIdAsync(int id)
    {
        var issue = await _dbSet
            .Include(i => i.Prescription).ThenInclude(p => p!.Client)
            .FirstOrDefaultAsync(i => i.Id == id);
        return issue == null ? null : EntityMapper.ToDto(issue);
    }

    public async Task<IEnumerable<PrescriptionIssueDto>> GetByPrescriptionIdAsync(int prescriptionId)
    {
        var issues = await _dbSet
            .Include(i => i.Prescription).ThenInclude(p => p!.Client)
            .Where(i => i.PrescriptionId == prescriptionId)
            .OrderByDescending(i => i.IssuedAt)
            .ToListAsync();
        return issues.Select(EntityMapper.ToDto);
    }

    public async Task<int> IssueAsync(int prescriptionId, IssuePrescriptionDto dto)
    {
        var prescription = await _context.Prescriptions
            .Include(p => p.Issues)
            .FirstOrDefaultAsync(p => p.Id == prescriptionId);
        if (prescription == null)
            throw new InvalidOperationException($"Prescription with ID {prescriptionId} not found");
        if (prescription.Status != PrescriptionStatus.Active)
            throw new InvalidOperationException($"Prescription {prescriptionId} is {prescription.Status} and can no longer be issued.");

        var practitioner = await _context.Practitioners.FindAsync(dto.IssuedByPractitionerId);
        if (practitioner == null)
            throw new InvalidOperationException($"Practitioner with ID {dto.IssuedByPractitionerId} not found");

        // HpiiNumber/Qualifications live on ApplicationUser, not Practitioner — resolved here (not
        // via UserManager, which is an Api/Identity-layer concern; AppDbContext already exposes Users
        // via IdentityDbContext) and null-safe, since Practitioner.ApplicationUserId is an unenforced
        // link that may be unset or point at nothing.
        ApplicationUser? applicationUser = null;
        if (!string.IsNullOrEmpty(practitioner.ApplicationUserId))
        {
            applicationUser = await _context.Users.FindAsync(practitioner.ApplicationUserId);
        }

        var issue = new PrescriptionIssue
        {
            PrescriptionId = prescriptionId,
            IssuedAt = DateTime.UtcNow,
            IssuedByPractitionerId = dto.IssuedByPractitionerId,
            RepeatNumber = prescription.Issues.Count,
            DeliveryMethod = dto.DeliveryMethod,
            TransmissionStatus = dto.DeliveryMethod == PrescriptionDeliveryMethod.Paper
                ? PrescriptionTransmissionStatus.NotApplicable
                : PrescriptionTransmissionStatus.Pending,

            // Snapshot of whatever the Prescription currently says — see PrescriptionIssue.cs's class
            // doc comment for why this is duplicated rather than read live at print time.
            Dose = prescription.Dose,
            Frequency = prescription.Frequency,
            Route = prescription.Route,
            Quantity = prescription.Quantity,
            Repeats = prescription.Repeats,
            PrescribingNotes = prescription.PrescribingNotes,
            NotesForPharmacy = prescription.NotesForPharmacy,

            PrescriberName = $"{practitioner.Title} {practitioner.FirstName} {practitioner.LastName}".Trim(),
            PrescriberProviderNumber = practitioner.ProviderNumber,
            PrescriberAhpraNumber = practitioner.AhpraNumber,
            PrescriberHpiiNumber = applicationUser?.HpiiNumber,
            PrescriberQualifications = applicationUser?.Qualifications
        };

        await _dbSet.AddAsync(issue);
        await _context.SaveChangesAsync();
        return issue.Id;
    }
}

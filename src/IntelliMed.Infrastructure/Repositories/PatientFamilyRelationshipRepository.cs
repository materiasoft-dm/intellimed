using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class PatientFamilyRelationshipRepository : Repository<PatientFamilyRelationship>, IPatientFamilyRelationshipRepository
{
    public PatientFamilyRelationshipRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<PatientFamilyRelationshipDto>> GetByPatientIdAsync(int patientId)
    {
        var relationships = await _dbSet
            .Where(r => r.PatientId == patientId)
            .Include(r => r.RelativePatient)
            .ToListAsync();

        return relationships.Select(r => new PatientFamilyRelationshipDto
        {
            Id = r.Id,
            PatientId = r.PatientId,
            RelativePatientId = r.RelativePatientId,
            RelativeName = r.RelativePatient != null ? $"{r.RelativePatient.FirstName} {r.RelativePatient.LastName}" : string.Empty,
            RelativeAddress = r.RelativePatient != null
                ? $"{r.RelativePatient.Address}, {r.RelativePatient.Suburb} {r.RelativePatient.State} {r.RelativePatient.Postcode}"
                : string.Empty,
            RelationshipType = r.RelationshipType,
            CreatedAt = r.CreatedAt
        });
    }

    public async Task<int> CreateAsync(CreatePatientFamilyRelationshipDto dto)
    {
        var relationship = new PatientFamilyRelationship
        {
            PatientId = dto.PatientId,
            RelativePatientId = dto.RelativePatientId,
            RelationshipType = dto.RelationshipType,
            CreatedAt = DateTime.UtcNow
        };
        await _dbSet.AddAsync(relationship);
        await _context.SaveChangesAsync();
        return relationship.Id;
    }
}

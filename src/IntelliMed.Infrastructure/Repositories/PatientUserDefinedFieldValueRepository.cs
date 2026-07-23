using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class PatientUserDefinedFieldValueRepository : Repository<PatientUserDefinedFieldValue>, IPatientUserDefinedFieldValueRepository
{
    public PatientUserDefinedFieldValueRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PatientUserDefinedFieldValueDto?> GetByIdAsync(int id)
    {
        var value = await _dbSet.Include(v => v.UserDefinedFieldType).FirstOrDefaultAsync(v => v.Id == id);
        return value == null ? null : ToDto(value);
    }

    public async Task<IEnumerable<PatientUserDefinedFieldValueDto>> GetByPatientIdAsync(int patientId)
    {
        var values = await _dbSet
            .Where(v => v.PatientId == patientId)
            .Include(v => v.UserDefinedFieldType)
            .ToListAsync();
        return values.Select(ToDto);
    }

    public async Task<int> CreateAsync(CreatePatientUserDefinedFieldValueDto dto)
    {
        var value = new PatientUserDefinedFieldValue
        {
            PatientId = dto.PatientId,
            UserDefinedFieldTypeId = dto.UserDefinedFieldTypeId,
            Value = dto.Value,
            Note = dto.Note,
            IsDefault = dto.IsDefault,
            CreatedAt = DateTime.UtcNow
        };
        await _dbSet.AddAsync(value);
        await _context.SaveChangesAsync();
        return value.Id;
    }

    public async Task UpdateAsync(int id, UpdatePatientUserDefinedFieldValueDto dto)
    {
        var value = await _dbSet.FindAsync(id);
        if (value == null)
            throw new InvalidOperationException($"PatientUserDefinedFieldValue with ID {id} not found");

        value.Value = dto.Value;
        value.Note = dto.Note;
        value.IsDefault = dto.IsDefault;
        value.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    private static PatientUserDefinedFieldValueDto ToDto(PatientUserDefinedFieldValue entity) => new()
    {
        Id = entity.Id,
        PatientId = entity.PatientId,
        UserDefinedFieldTypeId = entity.UserDefinedFieldTypeId,
        FieldName = entity.UserDefinedFieldType?.Name ?? string.Empty,
        FieldType = entity.UserDefinedFieldType?.FieldType ?? Core.Entities.UdfFieldTypeEnum.Text,
        Value = entity.Value,
        Note = entity.Note,
        IsDefault = entity.IsDefault,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}

using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ClientUserDefinedFieldValueRepository : Repository<ClientUserDefinedFieldValue>, IClientUserDefinedFieldValueRepository
{
    public ClientUserDefinedFieldValueRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ClientUserDefinedFieldValueDto?> GetByIdAsync(int id)
    {
        var value = await _dbSet.Include(v => v.UserDefinedFieldType).FirstOrDefaultAsync(v => v.Id == id);
        return value == null ? null : ToDto(value);
    }

    public async Task<IEnumerable<ClientUserDefinedFieldValueDto>> GetByClientIdAsync(int clientId)
    {
        var values = await _dbSet
            .Where(v => v.ClientId == clientId)
            .Include(v => v.UserDefinedFieldType)
            .ToListAsync();
        return values.Select(ToDto);
    }

    public async Task<int> CreateAsync(CreateClientUserDefinedFieldValueDto dto)
    {
        var value = new ClientUserDefinedFieldValue
        {
            ClientId = dto.ClientId,
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

    public async Task UpdateAsync(int id, UpdateClientUserDefinedFieldValueDto dto)
    {
        var value = await _dbSet.FindAsync(id);
        if (value == null)
            throw new InvalidOperationException($"ClientUserDefinedFieldValue with ID {id} not found");

        value.Value = dto.Value;
        value.Note = dto.Note;
        value.IsDefault = dto.IsDefault;
        value.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    private static ClientUserDefinedFieldValueDto ToDto(ClientUserDefinedFieldValue entity) => new()
    {
        Id = entity.Id,
        ClientId = entity.ClientId,
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

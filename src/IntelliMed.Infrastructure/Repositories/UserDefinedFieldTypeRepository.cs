using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class UserDefinedFieldTypeRepository : Repository<UserDefinedFieldType>, IUserDefinedFieldTypeRepository
{
    public UserDefinedFieldTypeRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<UserDefinedFieldTypeDto?> GetByIdAsync(int id)
    {
        var fieldType = await _dbSet.FindAsync(id);
        return fieldType == null ? null : EntityMapper.ToDto(fieldType);
    }

    public async Task<IEnumerable<UserDefinedFieldTypeDto>> GetAllActiveAsync()
    {
        var fieldTypes = await _dbSet
            .Where(f => f.IsActive)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync();
        return fieldTypes.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateUserDefinedFieldTypeDto dto)
    {
        var fieldType = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(fieldType);
        await _context.SaveChangesAsync();
        return fieldType.Id;
    }

    public async Task UpdateAsync(int id, UpdateUserDefinedFieldTypeDto dto)
    {
        var fieldType = await _dbSet.FindAsync(id);
        if (fieldType == null)
            throw new InvalidOperationException($"UserDefinedFieldType with ID {id} not found");

        EntityMapper.UpdateEntity(fieldType, dto);
        await _context.SaveChangesAsync();
    }
}

using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IUserDefinedFieldTypeRepository : IRepository<UserDefinedFieldType>
{
    Task<UserDefinedFieldTypeDto?> GetByIdAsync(int id);
    Task<IEnumerable<UserDefinedFieldTypeDto>> GetAllActiveAsync();
    Task<int> CreateAsync(CreateUserDefinedFieldTypeDto dto);
    Task UpdateAsync(int id, UpdateUserDefinedFieldTypeDto dto);
}

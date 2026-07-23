using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IUdfDefinitionService
{
    Task<IEnumerable<UserDefinedFieldTypeDto>> GetAllActiveAsync();
}

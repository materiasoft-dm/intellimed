using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IProviderGroupService
{
    Task<IEnumerable<ProviderGroupDto>> GetAllActiveAsync();
}

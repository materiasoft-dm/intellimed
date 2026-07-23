using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IHealthFundService
{
    Task<IEnumerable<HealthFundDto>> GetAllActiveAsync();
}

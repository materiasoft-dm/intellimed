using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IHealthFundRepository : IRepository<HealthFund>
{
    new Task<HealthFundDto?> GetByIdAsync(int id);
    Task<IEnumerable<HealthFundDto>> GetAllActiveAsync();
}

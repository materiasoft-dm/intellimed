using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IBillingItemRepository : IRepository<BillingItem>
{
    Task<BillingItemDto?> GetByIdAsync(int id);
    Task<IEnumerable<BillingItemDto>> SearchAsync(string query, int take = 20);
}

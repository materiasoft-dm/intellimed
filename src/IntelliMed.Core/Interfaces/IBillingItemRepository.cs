using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IBillingItemRepository : IRepository<BillingItem>
{
    new Task<BillingItemDto?> GetByIdAsync(int id);
    Task<IEnumerable<BillingItemDto>> SearchAsync(string query, int take = 20);
}

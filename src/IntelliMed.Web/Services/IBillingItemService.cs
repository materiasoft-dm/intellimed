using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IBillingItemService
{
    Task<List<BillingItemDto>> SearchAsync(string query);
    Task<BillingItemSyncResultDto?> SyncAsync();
}

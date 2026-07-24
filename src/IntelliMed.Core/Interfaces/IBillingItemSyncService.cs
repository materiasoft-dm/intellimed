using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

public interface IBillingItemSyncService
{
    Task<BillingItemSyncResultDto> SyncAsync();
}

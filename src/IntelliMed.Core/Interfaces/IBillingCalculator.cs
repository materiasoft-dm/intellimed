using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

/// <summary>
/// Resolves a line item's charged fee, bulk-bill-equivalent rebate and GST for a billing context,
/// mirroring legacy Pracnet's IRebateCalculator (BulkBill vs Private rebate rules).
/// </summary>
public interface IBillingCalculator
{
    Task<ResolveLineResult> ResolveLineAsync(
        int clinicId,
        AccountTypeEnum accountType,
        ProviderServiceType providerServiceType,
        PlaceOfServiceEnum placeOfService,
        int billingItemId,
        DateTime? serviceDate,
        int? healthFundId = null,
        int? overrideFeeScheduleId = null);

    /// <summary>Resolves the same charged fee schedule ResolveLineAsync would use, for callers (e.g. the derived-item engine) that need it without resolving a specific line.</summary>
    Task<int?> ResolveFeeScheduleIdAsync(int clinicId, AccountTypeEnum accountType, int? healthFundId = null);
}

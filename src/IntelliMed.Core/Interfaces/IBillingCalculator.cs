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
        int? healthFundId = null);
}

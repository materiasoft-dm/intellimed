namespace IntelliMed.Core.Entities;

/// <summary>
/// Provider service type, used when resolving the bulk-bill-equivalent rebate.
/// Maps to legacy Pracnet service-type codes: GeneralPractitioner = "O", Specialist = "S", Pathology = "P".
/// </summary>
public enum ProviderServiceType
{
    GeneralPractitioner,
    Specialist,
    Pathology
}

/// <summary>
/// Where the service was rendered. Drives BBO (rooms) vs BBI (hospital) rebate selection.
/// Legacy uses locationId "H" for Hospital.
/// </summary>
public enum PlaceOfServiceEnum
{
    Rooms,
    Hospital
}

/// <summary>
/// How a derived item's fee is computed from other items on the same invoice, ported field-for-field
/// from legacy's DerivedFeeCalculator (Abaki.Billing.Bll). 8 of legacy's 10 strategies are implemented;
/// AdditionalFoetusTested and OriginalAmputationFee are omitted because they are empty method bodies
/// in the legacy source itself (never actually implemented there either).
/// PercentageOfAssociatedItem and ExcisionMalignantTumour compute identically — both sum
/// (Percentage x siblingFee)/100 over the matched association list, then apply the fee-schedule
/// override/gap rule; kept as separate labels only because they're distinct legacy config rows, not
/// because the formula differs.
/// </summary>
public enum DerivedCalculationType
{
    /// <summary>Fee = sum of (Percentage x siblingFee)/100 over every sibling matching the configured association, then fee-schedule override/gap.</summary>
    PercentageOfAssociatedItem,

    /// <summary>Same formula as PercentageOfAssociatedItem — a separate label for legacy config-row parity.</summary>
    ExcisionMalignantTumour,

    /// <summary>Fee = sum of (Percentage x siblingFee)/100 over every OTHER sibling line, no association filter, no schedule override/gap.</summary>
    ProcedureDiscontinued,

    /// <summary>Fee = sum of (PercentageFromInvoiceTotal x siblingFee)/100 over every other sibling line (excludes itself), then fee-schedule override/gap.</summary>
    CombinationOperations,

    /// <summary>Gated on both Group A and Group B having a matching sibling (else fee = 0); when open, sums (Percentage x siblingFee)/100 over every sibling matching any of Groups A-D, then fee-schedule override/gap.</summary>
    AssistanceAnaesthesia,

    /// <summary>Gated on Group A having a matching sibling (else fee = 0); when open, Fee = CalculateFromFee + sum of sibling fees matching Group A/B/C.</summary>
    BasicUnits,

    /// <summary>3-path: (A) CalculateFromFee as base, (B) DerivedItemRateCalculated precalculated override keyed by patient count, (C) associated item's own fee as base. All paths: fee = base + (patientCount &lt;= NumOfLimitPatient ? UnderNumOfLimitPatientPlusTotal/patientCount : OverNumOfLimitPatientPlus), then x gapPercent/100 if applicable.</summary>
    NumberOfPatientsSeen,

    /// <summary>Base = associated item's fee. Checks DerivedItemRateCalculated precalculated override (keyed by quantity) first. Else: if quantity &gt; NumberOfLimitQuantity, add (quantity - limit) x OverLimitQuantityPlus; then x gap percent if applicable.</summary>
    FieldQuantity,

    /// <summary>Base = associated item's fee. Always adds quantity x OverLimitQuantityPlus (no threshold subtraction, unlike FieldQuantity), then x gap percent if applicable.</summary>
    TimeDuration
}

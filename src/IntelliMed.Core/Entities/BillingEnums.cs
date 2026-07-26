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
/// How a derived item's fee is computed from other items on the same invoice, mirroring (a scoped
/// subset of) legacy's 10-strategy DerivedFeeCalculator. PercentageOfAssociatedItem/AssistanceAnaesthesia
/// scale off a sibling line's fee; BasicUnits/FieldQuantity/TimeDuration/NumberOfPatientsSeen scale a
/// per-line manually-entered quantity (InvoiceItem.DerivedQuantity).
/// </summary>
public enum DerivedCalculationType
{
    PercentageOfAssociatedItem,
    AssistanceAnaesthesia,
    BasicUnits,
    FieldQuantity,
    TimeDuration,
    NumberOfPatientsSeen
}

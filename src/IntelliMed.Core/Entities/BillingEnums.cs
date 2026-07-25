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

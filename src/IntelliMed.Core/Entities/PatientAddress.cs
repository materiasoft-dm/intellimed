namespace IntelliMed.Core.Entities;

public enum PatientAddressType
{
    Postal,
    Other
}

public class PatientAddress
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public PatientAddressType AddressType { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string Suburb { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AddressSubType { get; set; } = "Not Specified";
    public string? Community { get; set; }
    public bool SendToMedicare { get; set; }

    public Patient? Patient { get; set; }
}

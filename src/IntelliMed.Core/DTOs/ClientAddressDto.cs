using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class ClientAddressDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public ClientAddressType AddressType { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string Suburb { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AddressSubType { get; set; } = "Not Specified";
    public string? Community { get; set; }
    public bool SendToMedicare { get; set; }
}

public class CreateClientAddressDto
{
    public int ClientId { get; set; }
    public ClientAddressType AddressType { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string Suburb { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AddressSubType { get; set; } = "Not Specified";
    public string? Community { get; set; }
    public bool SendToMedicare { get; set; }
}

public class UpdateClientAddressDto
{
    public ClientAddressType AddressType { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string Suburb { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AddressSubType { get; set; } = "Not Specified";
    public string? Community { get; set; }
    public bool SendToMedicare { get; set; }
}

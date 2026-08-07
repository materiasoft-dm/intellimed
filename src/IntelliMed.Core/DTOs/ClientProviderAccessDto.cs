namespace IntelliMed.Core.DTOs;

public class ClientProviderAccessDto
{
    public int PractitionerId { get; set; }
    public string PractitionerName { get; set; } = string.Empty;

    /// <summary>True for the practitioner who created this client — always present, never removable.</summary>
    public bool IsCreator { get; set; }
}

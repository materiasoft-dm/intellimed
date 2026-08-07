namespace IntelliMed.Core.Entities;

/// <summary>An explicit grant letting one practitioner see one client, beyond the always-implicit
/// creator (Client.CreatedByPractitionerId). Only consulted when Client.VisibleToAllProviders is
/// false — see ClientRepository.BuildSearchQuery.</summary>
public class ClientProviderAccess
{
    public int Id { get; set; }

    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public int PractitionerId { get; set; }
    public Practitioner? Practitioner { get; set; }

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}

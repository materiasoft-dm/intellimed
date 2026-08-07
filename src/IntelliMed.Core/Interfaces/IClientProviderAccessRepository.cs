using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

public interface IClientProviderAccessRepository
{
    Task<List<ClientProviderAccessDto>> GetForClientAsync(int clientId);

    /// <summary>Replaces every explicit grant for this client with the given set of practitioner
    /// ids. The client's CreatedByPractitionerId (if any) is always forced into the set — the
    /// creator's own grant can never be removed this way, even if the caller omits it.</summary>
    Task SetForClientAsync(int clientId, IEnumerable<int> practitionerIds);
}

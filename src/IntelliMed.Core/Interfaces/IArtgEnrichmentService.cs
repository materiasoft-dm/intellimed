using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

public interface IArtgEnrichmentService
{
    /// <summary>Reads a TGA ARTG "Medicines" export and backfills poisons schedule, ARTG ID, route
    /// and dosage form onto matching catalogue rows. Only fills fields that are currently empty — it
    /// never overwrites a value a sync or an admin already set.</summary>
    Task<ArtgEnrichmentResultDto> EnrichAsync(TextReader csv, CancellationToken cancellationToken = default);
}

namespace IntelliMed.Core.DTOs;

/// <summary>Outcome of an ARTG enrichment pass. Deliberately reports matched-but-unchanged and
/// unmatched separately: "nothing changed" after a second run is success, whereas a large unmatched
/// count means the brand matching needs work, and one number can't say both.</summary>
public class ArtgEnrichmentResultDto
{
    /// <summary>Medicine rows considered (active catalogue entries).</summary>
    public int CatalogueRows { get; set; }

    /// <summary>ARTG rows parsed out of the upload.</summary>
    public int ArtgRows { get; set; }

    /// <summary>Catalogue rows matched to at least one ARTG entry by brand.</summary>
    public int Matched { get; set; }

    public int ScheduleFilled { get; set; }
    public int ArtgIdFilled { get; set; }
    public int RouteFilled { get; set; }
    public int FormFilled { get; set; }

    /// <summary>Matched, but every target field was already populated — nothing to do.</summary>
    public int AlreadyComplete { get; set; }

    /// <summary>No ARTG brand matched. Expected for generic-name rows from non-PBS sources.</summary>
    public int Unmatched { get; set; }

    /// <summary>How many rows ended up on each schedule, for a sanity check against the export's own
    /// distribution — a wildly different S8 count means the mapping or matching went wrong.</summary>
    public Dictionary<string, int> ScheduleBreakdown { get; set; } = new();

    public DateTime CompletedAt { get; set; }
    public string? Message { get; set; }
}

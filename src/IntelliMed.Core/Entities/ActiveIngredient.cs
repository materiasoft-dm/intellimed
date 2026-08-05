namespace IntelliMed.Core.Entities;

public class ActiveIngredient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Set once this ingredient has been matched/confirmed against an AMT substance concept.
    /// False for ingredients an importer had to auto-create from a name it couldn't match — the
    /// signal that a row needs review rather than being trusted as clinically curated.</summary>
    public string? AmtSubstanceId { get; set; }
    public bool IsAmtConfirmed { get; set; }

    // Wikidata enrichment — identification-layer cross-references only (ATC code, CC0-licensed),
    // not a clinical authority like AMT. WikidataId is the Q-id (e.g. "Q57055"), kept for
    // traceability/re-sync rather than display.
    public string? AtcCode { get; set; }
    public string? WikidataId { get; set; }
}

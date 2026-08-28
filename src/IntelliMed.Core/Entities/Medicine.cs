namespace IntelliMed.Core.Entities;

public class Medicine
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Strength { get; set; }
    public string? Form { get; set; }
    // Distinguishes real PBS items that otherwise share Name+Strength+Form — PBS assigns a
    // different item code per pack quantity (see MedicineCsvAssemblyService's MatchKey comment),
    // so this must be part of the identity key, not just a display detail.
    public string? PackSize { get; set; }
    public string? Manufacturer { get; set; }
    public bool IsPbsListed { get; set; }
    public bool IsRpbsListed { get; set; }
    public PbsRestrictionLevel? RestrictionLevel { get; set; }
    public MedicineSchedule? Schedule { get; set; }
    public MedicineSource Source { get; set; } = MedicineSource.Manual;

    // Provenance — populated by whichever source(s) contributed this row. All nullable: a row can
    // start from a self-compiled starter list with none of these set, and later get enriched once
    // an AMT/PBS/ARTG feed is actually usable (see plan doc for the licensing status of each).
    public string? ArtgId { get; set; }
    public string? AmtConceptId { get; set; }
    public string? PbsItemCode { get; set; }
    public string? AtcCode { get; set; }

    // ---- PBS prescribing limits and classification -------------------------
    // All from the PBS `items` endpoint and stored verbatim, same rule as every other PBS field.
    // Nullable throughout: a manually-created medicine or a Wikidata-sourced row has none of this.

    /// <summary>PBS `number_of_repeats` — the maximum repeats the listing allows. Legacy Medinet's
    /// MAX_RP. Drives the wizard's default repeat count and half of its overdose check.</summary>
    public int? MaxRepeats { get; set; }

    /// <summary>PBS `maximum_quantity_units`. The other half of the overdose check.</summary>
    public int? MaxQuantity { get; set; }

    /// <summary>PBS `moa_preferred_term` (e.g. "Oral") — the display form of
    /// `manner_of_administration`. Pre-fills the wizard's Route field, which was free text before.</summary>
    public string? Route { get; set; }

    /// <summary>PBS `section100_only_indicator`. Section 100 items are Authority-only, so this is a
    /// routing trigger in its own right, independent of RestrictionLevel — see legacy §3.</summary>
    public bool IsSection100 { get; set; }

    /// <summary>PBS `therapeutic_group_id` / `therapeutic_group_title`. Two medicines sharing a
    /// group are therapeutically equivalent, which is what duplicate-therapy checking keys off.
    /// Frequently null — PBS only assigns groups where an equivalence exists.</summary>
    public string? TherapeuticGroupId { get; set; }
    public string? TherapeuticGroupTitle { get; set; }

    // Never hard-deleted, same rule as BillingItem — ClientMedication will FK to this later.
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MedicineIngredient> Ingredients { get; set; } = new List<MedicineIngredient>();
}

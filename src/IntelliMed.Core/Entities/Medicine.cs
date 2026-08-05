namespace IntelliMed.Core.Entities;

public class Medicine
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Strength { get; set; }
    public string? Form { get; set; }
    public string? Manufacturer { get; set; }
    public bool IsPbsListed { get; set; }
    public MedicineSchedule? Schedule { get; set; }
    public MedicineSource Source { get; set; } = MedicineSource.Manual;

    // Provenance — populated by whichever source(s) contributed this row. All nullable: a row can
    // start from a self-compiled starter list with none of these set, and later get enriched once
    // an AMT/PBS/ARTG feed is actually usable (see plan doc for the licensing status of each).
    public string? ArtgId { get; set; }
    public string? AmtConceptId { get; set; }
    public string? PbsItemCode { get; set; }
    public string? AtcCode { get; set; }

    // Never hard-deleted, same rule as BillingItem — ClientMedication will FK to this later.
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MedicineIngredient> Ingredients { get; set; } = new List<MedicineIngredient>();
}

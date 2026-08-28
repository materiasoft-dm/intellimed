using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class MedicineDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Strength { get; set; }
    public string? Form { get; set; }
    public string? PackSize { get; set; }
    public string? Manufacturer { get; set; }
    public bool IsPbsListed { get; set; }
    public bool IsRpbsListed { get; set; }
    public PbsRestrictionLevel? RestrictionLevel { get; set; }
    public MedicineSchedule? Schedule { get; set; }
    public MedicineSource Source { get; set; }
    public string? ArtgId { get; set; }
    public string? AmtConceptId { get; set; }
    public string? PbsItemCode { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public List<IngredientRefDto> ActiveIngredients { get; set; } = new();

    // PBS prescribing limits and classification — see Medicine.cs.
    public int? MaxRepeats { get; set; }
    public int? MaxQuantity { get; set; }
    public string? Route { get; set; }
    public bool IsSection100 { get; set; }
    public string? TherapeuticGroupId { get; set; }
    public string? TherapeuticGroupTitle { get; set; }

    /// <summary>True when this row carries no poisons schedule. Distinct from "unscheduled" — it
    /// means unknown, because the PBS feed doesn't publish scheduling at all (see
    /// MEDICINE_DATA_ENRICHMENT.md §1). The UI must say so rather than render nothing, so absence
    /// isn't misread as "not S8".</summary>
    public bool SchedulingUnknown => Schedule == null;
}

public class IngredientRefDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AtcCode { get; set; }
}

public class CreateMedicineDto
{
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Strength { get; set; }
    public string? Form { get; set; }
    public string? PackSize { get; set; }
    public string? Manufacturer { get; set; }
    public bool IsPbsListed { get; set; }
    public bool IsRpbsListed { get; set; }
    public PbsRestrictionLevel? RestrictionLevel { get; set; }
    public MedicineSchedule? Schedule { get; set; }

    /// <summary>Semicolon-separated ingredient names, same convention as the CSV import — each is
    /// matched against an existing ActiveIngredient by name or created new.</summary>
    public string? ActiveIngredientNames { get; set; }
}

public class UpdateMedicineDto
{
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Strength { get; set; }
    public string? Form { get; set; }
    public string? PackSize { get; set; }
    public string? Manufacturer { get; set; }
    public bool IsPbsListed { get; set; }
    public bool IsRpbsListed { get; set; }
    public PbsRestrictionLevel? RestrictionLevel { get; set; }
    public MedicineSchedule? Schedule { get; set; }
    public string? ActiveIngredientNames { get; set; }
    public bool IsActive { get; set; }
}

public class MedicineImportResultDto
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Unmatched { get; set; }
    public int Deactivated { get; set; }
    public DateTime ImportedAt { get; set; }
    public string? Message { get; set; }
}

public class IngredientEnrichmentResultDto
{
    public int Enriched { get; set; }
    public int NotFound { get; set; }
    public int TotalProcessed { get; set; }
    public DateTime CompletedAt { get; set; }
}

/// <summary>Polled by the Web layer while a Sync job (assemble -> import -> enrich) runs in the
/// background. ImportResult/EnrichmentResult stay null until the job reaches that phase — a job that
/// fails or is cancelled during the source-fetch phase completes with both still null.</summary>
public class SyncProgressDto
{
    public bool IsComplete { get; set; }
    public bool IsCancelled { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Log { get; set; } = new();
    public string? Error { get; set; }
    public MedicineImportResultDto? ImportResult { get; set; }
    public IngredientEnrichmentResultDto? EnrichmentResult { get; set; }
}

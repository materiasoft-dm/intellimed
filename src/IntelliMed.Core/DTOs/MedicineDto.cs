using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class MedicineDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Strength { get; set; }
    public string? Form { get; set; }
    public string? Manufacturer { get; set; }
    public bool IsPbsListed { get; set; }
    public MedicineSchedule? Schedule { get; set; }
    public MedicineSource Source { get; set; }
    public string? ArtgId { get; set; }
    public string? AmtConceptId { get; set; }
    public string? PbsItemCode { get; set; }
    public bool IsActive { get; set; }
    public List<IngredientRefDto> ActiveIngredients { get; set; } = new();
}

public class IngredientRefDto
{
    public string Name { get; set; } = string.Empty;
    public string? AtcCode { get; set; }
}

public class CreateMedicineDto
{
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Strength { get; set; }
    public string? Form { get; set; }
    public string? Manufacturer { get; set; }
    public bool IsPbsListed { get; set; }
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
    public string? Manufacturer { get; set; }
    public bool IsPbsListed { get; set; }
    public MedicineSchedule? Schedule { get; set; }
    public string? ActiveIngredientNames { get; set; }
    public bool IsActive { get; set; }
}

public class MedicineImportResultDto
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Unmatched { get; set; }
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

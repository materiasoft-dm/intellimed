using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class DrugInteractionDto
{
    public int Id { get; set; }
    public int IngredientAId { get; set; }
    public string IngredientAName { get; set; } = string.Empty;
    public int IngredientBId { get; set; }
    public string IngredientBName { get; set; } = string.Empty;
    public InteractionSeverity Severity { get; set; }
    public string? Description { get; set; }
    public string? SourceDataset { get; set; }
    public bool IsActive { get; set; }
}

public class CreateDrugInteractionDto
{
    /// <summary>Matched against an existing ActiveIngredient by name, or created new — same
    /// find-or-create convention as Medicine's ActiveIngredientNames field.</summary>
    public string IngredientAName { get; set; } = string.Empty;
    public string IngredientBName { get; set; } = string.Empty;
    public InteractionSeverity Severity { get; set; }
    public string? Description { get; set; }
    public string? SourceDataset { get; set; }
}

public class UpdateDrugInteractionDto
{
    public InteractionSeverity Severity { get; set; }
    public string? Description { get; set; }
    public string? SourceDataset { get; set; }
    public bool IsActive { get; set; }
}

public class DrugInteractionImportResultDto
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int IngredientsAutoCreated { get; set; }
    public DateTime ImportedAt { get; set; }
    public string? Message { get; set; }
}

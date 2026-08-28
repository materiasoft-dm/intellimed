namespace IntelliMed.Core.Entities;

/// <summary>Reference data — an admin-imported known interaction between two active ingredients.
/// IngredientAId/IngredientBId are always stored canonically ordered (A &lt; B) by whoever writes the
/// row (see DrugInteractionRepository), so the unique compound index on (IngredientAId, IngredientBId)
/// actually enforces "no duplicate pair" regardless of which order a source lists them in.
/// Ships empty by default — the obvious open dataset (DDInter) is CC-BY-NC and needs explicit
/// permission from Oxford University Press before it can be used here; see the plan doc. Populate via
/// DrugInteractionsAdmin's CSV import once a properly licensed source is identified.</summary>
public class DrugInteraction
{
    public int Id { get; set; }
    public int IngredientAId { get; set; }
    public int IngredientBId { get; set; }
    public InteractionSeverity Severity { get; set; }
    public string? Description { get; set; }

    /// <summary>Traceability — which dataset/version this row came from. Null/empty is a valid,
    /// expected state (self-curated or unknown-provenance rows), not an error.</summary>
    public string? SourceDataset { get; set; }

    public bool IsActive { get; set; } = true;

    public ActiveIngredient? IngredientA { get; set; }
    public ActiveIngredient? IngredientB { get; set; }
}

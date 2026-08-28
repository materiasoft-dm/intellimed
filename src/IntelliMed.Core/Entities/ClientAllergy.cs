namespace IntelliMed.Core.Entities;

public class ClientAllergy
{
    public int Id { get; set; }
    public int ClientId { get; set; }

    /// <summary>Cross-check key for MedicationInteractionChecker's allergy matching — nullable because
    /// reception/nursing staff commonly record an allergy as free text without picking a catalog
    /// ingredient row. See Substance.</summary>
    public int? ActiveIngredientId { get; set; }

    /// <summary>Always populated, even when ActiveIngredientId is set — covers non-drug allergies
    /// (e.g. latex) and is also the checker's fallback text-match target when the FK isn't set.</summary>
    public string Substance { get; set; } = string.Empty;

    public AllergyType Type { get; set; } = AllergyType.Medication;
    public string? Reaction { get; set; }
    public AllergySeverity? Severity { get; set; }
    public DateTime? OnsetDate { get; set; }
    public string? Note { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public ActiveIngredient? ActiveIngredient { get; set; }
}

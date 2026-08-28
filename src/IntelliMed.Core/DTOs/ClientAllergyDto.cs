using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class ClientAllergyDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int? ActiveIngredientId { get; set; }
    public string? ActiveIngredientName { get; set; }
    public string Substance { get; set; } = string.Empty;
    public AllergyType Type { get; set; }
    public string? Reaction { get; set; }
    public AllergySeverity? Severity { get; set; }
    public DateTime? OnsetDate { get; set; }
    public string? Note { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateClientAllergyDto
{
    public int ClientId { get; set; }
    public int? ActiveIngredientId { get; set; }
    public string Substance { get; set; } = string.Empty;
    public AllergyType Type { get; set; } = AllergyType.Medication;
    public string? Reaction { get; set; }
    public AllergySeverity? Severity { get; set; }
    public DateTime? OnsetDate { get; set; }
    public string? Note { get; set; }
}

public class UpdateClientAllergyDto
{
    public int? ActiveIngredientId { get; set; }
    public string Substance { get; set; } = string.Empty;
    public AllergyType Type { get; set; }
    public string? Reaction { get; set; }
    public AllergySeverity? Severity { get; set; }
    public DateTime? OnsetDate { get; set; }
    public string? Note { get; set; }
    public bool IsArchived { get; set; }
}

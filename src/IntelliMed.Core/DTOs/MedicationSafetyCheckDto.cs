using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

/// <summary>Result of checking a candidate medicine against a client's active prescriptions and
/// allergies before it's issued — see IMedicationInteractionChecker. Always non-blocking: the caller
/// decides whether to proceed, this only informs that decision.</summary>
public class MedicationSafetyCheckResultDto
{
    public List<DrugInteractionWarningDto> InteractionWarnings { get; set; } = new();
    public List<AllergyWarningDto> AllergyWarnings { get; set; } = new();
    public List<DuplicateTherapyWarningDto> DuplicateTherapyWarnings { get; set; } = new();

    /// <summary>True when DrugInteraction has zero reference rows loaded at all — distinguishes "we
    /// checked and found nothing" from "there was nothing to check against," so the UI doesn't show a
    /// false-reassuring clean result.</summary>
    public bool InteractionDatabaseEmpty { get; set; }

    /// <summary>False when the candidate has no MedicineId (free-text/compounded item) — nothing to
    /// resolve ingredients from, so neither warning list can be trusted as complete.</summary>
    public bool CanFullyCheck { get; set; } = true;

    public bool HasWarnings =>
        InteractionWarnings.Count > 0 || AllergyWarnings.Count > 0 || DuplicateTherapyWarnings.Count > 0;
}

public class DrugInteractionWarningDto
{
    /// <summary>The client's existing active Prescription this candidate conflicts with.</summary>
    public string ConflictingMedicineName { get; set; } = string.Empty;
    public string IngredientAName { get; set; } = string.Empty;
    public string IngredientBName { get; set; } = string.Empty;
    public InteractionSeverity Severity { get; set; }
    public string? Description { get; set; }
}

/// <summary>The client is already on a medicine PBS considers therapeutically equivalent to the one
/// being prescribed. Advisory like every other check here — switching brands or overlapping during a
/// changeover are both legitimate, so this flags rather than blocks.</summary>
public class DuplicateTherapyWarningDto
{
    public string ExistingMedicineName { get; set; } = string.Empty;
    public string TherapeuticGroupTitle { get; set; } = string.Empty;
}

public class AllergyWarningDto
{
    public string AllergySubstance { get; set; } = string.Empty;
    public AllergySeverity? Severity { get; set; }

    /// <summary>True when matched via ActiveIngredientId (confirmed); false when matched only by a
    /// substring/fuzzy match against free-text Substance ("possible — please verify").</summary>
    public bool IsConfirmedMatch { get; set; }
}

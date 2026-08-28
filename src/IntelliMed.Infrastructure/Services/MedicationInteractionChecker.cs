using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

public class MedicationInteractionChecker : IMedicationInteractionChecker
{
    private readonly AppDbContext _context;
    private readonly IDrugInteractionRepository _interactionRepository;

    public MedicationInteractionChecker(AppDbContext context, IDrugInteractionRepository interactionRepository)
    {
        _context = context;
        _interactionRepository = interactionRepository;
    }

    public async Task<MedicationSafetyCheckResultDto> CheckAsync(MedicationSafetyCheckRequest request)
    {
        var result = new MedicationSafetyCheckResultDto
        {
            CanFullyCheck = request.CandidateIngredientIds.Count > 0
        };
        if (!result.CanFullyCheck) return result; // free-text/compounded item — nothing to resolve

        result.InteractionDatabaseEmpty = await _interactionRepository.CountAsync() == 0;

        await CheckDrugDrugAsync(request, result);
        await CheckDrugAllergyAsync(request, result);
        await CheckDuplicateTherapyAsync(request, result);

        return result;
    }

    /// <summary>
    /// Warns when the client is already on a medicine sharing the candidate's PBS therapeutic group
    /// — i.e. PBS considers them interchangeable. This is the one check that needs catalogue rows
    /// rather than resolved ingredient ids, because the grouping is a property of the listing.
    ///
    /// Silently does nothing when either side lacks a group, which is common: PBS only assigns
    /// therapeutic groups where an equivalence actually exists. Absence is not evidence of safety,
    /// same caveat as an empty interaction database.
    /// </summary>
    private async Task CheckDuplicateTherapyAsync(MedicationSafetyCheckRequest request, MedicationSafetyCheckResultDto result)
    {
        if (request.CandidateMedicineId is not { } candidateId) return;

        var candidateGroup = await _context.Medicines
            .Where(m => m.Id == candidateId)
            .Select(m => m.TherapeuticGroupId)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(candidateGroup)) return;

        // Exclude the candidate itself — re-prescribing the same medicine is a repeat, not a duplicate.
        var existingMedicineIds = request.ExistingMedicationIngredients
            .Where(e => e.MedicineId.HasValue && e.MedicineId.Value != candidateId)
            .Select(e => e.MedicineId!.Value)
            .Distinct()
            .ToList();
        if (existingMedicineIds.Count == 0) return;

        var duplicates = await _context.Medicines
            .Where(m => existingMedicineIds.Contains(m.Id) && m.TherapeuticGroupId == candidateGroup)
            .Select(m => new { m.Name, m.TherapeuticGroupTitle })
            .ToListAsync();

        foreach (var duplicate in duplicates)
        {
            result.DuplicateTherapyWarnings.Add(new DuplicateTherapyWarningDto
            {
                ExistingMedicineName = duplicate.Name,
                TherapeuticGroupTitle = duplicate.TherapeuticGroupTitle ?? string.Empty
            });
        }
    }

    private async Task CheckDrugDrugAsync(MedicationSafetyCheckRequest request, MedicationSafetyCheckResultDto result)
    {
        var existingIds = request.ExistingMedicationIngredients.Select(e => e.IngredientId).Distinct().ToList();
        var interactions = await _interactionRepository.FindInteractionsAsync(request.CandidateIngredientIds, existingIds);

        foreach (var interaction in interactions)
        {
            // Whichever side of the stored pair isn't one of the candidate's own ingredients is the
            // "existing medication" side — report which of the client's current medicines it came from.
            var existingIngredientId = request.CandidateIngredientIds.Contains(interaction.IngredientAId)
                ? interaction.IngredientBId
                : interaction.IngredientAId;

            var conflictingMedicineNames = request.ExistingMedicationIngredients
                .Where(e => e.IngredientId == existingIngredientId)
                .Select(e => e.MedicineName)
                .Distinct();

            foreach (var medicineName in conflictingMedicineNames)
            {
                result.InteractionWarnings.Add(new DrugInteractionWarningDto
                {
                    ConflictingMedicineName = medicineName,
                    IngredientAName = interaction.IngredientA?.Name ?? string.Empty,
                    IngredientBName = interaction.IngredientB?.Name ?? string.Empty,
                    Severity = interaction.Severity,
                    Description = interaction.Description
                });
            }
        }
    }

    private async Task CheckDrugAllergyAsync(MedicationSafetyCheckRequest request, MedicationSafetyCheckResultDto result)
    {
        var allergies = await _context.ClientAllergies
            .Where(a => a.ClientId == request.ClientId && !a.IsArchived)
            .ToListAsync();
        if (allergies.Count == 0) return;

        var candidateIngredientNames = await _context.ActiveIngredients
            .Where(i => request.CandidateIngredientIds.Contains(i.Id))
            .Select(i => i.Name)
            .ToListAsync();

        // What a text match is checked against — the candidate's brand/generic name plus each of its
        // ingredient names, so "penicillin" recorded as free text still catches "Amoxil" (brand) via
        // its ingredient, not just an exact name match.
        var textMatchTargets = new List<string> { request.CandidateMedicineName };
        if (request.CandidateGenericName != null) textMatchTargets.Add(request.CandidateGenericName);
        textMatchTargets.AddRange(candidateIngredientNames);

        foreach (var allergy in allergies)
        {
            var isConfirmed = allergy.ActiveIngredientId.HasValue &&
                               request.CandidateIngredientIds.Contains(allergy.ActiveIngredientId.Value);

            var isPossible = !isConfirmed && textMatchTargets.Any(t =>
                t.Contains(allergy.Substance, StringComparison.OrdinalIgnoreCase) ||
                allergy.Substance.Contains(t, StringComparison.OrdinalIgnoreCase));

            if (isConfirmed || isPossible)
            {
                result.AllergyWarnings.Add(new AllergyWarningDto
                {
                    AllergySubstance = allergy.Substance,
                    Severity = allergy.Severity,
                    IsConfirmedMatch = isConfirmed
                });
            }
        }
    }
}

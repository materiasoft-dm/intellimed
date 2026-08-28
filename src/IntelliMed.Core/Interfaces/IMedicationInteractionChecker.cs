using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

/// <summary>One of a client's currently-active medication ingredients, for drug-drug checking — one
/// entry per ingredient, so a combination product contributes multiple entries under the same
/// MedicineName. MedicineId is optional and only set for catalogue-backed medicines; duplicate-
/// therapy checking needs it to resolve the therapeutic group, and free-text items have none.</summary>
public record ExistingMedicationIngredient(int IngredientId, string MedicineName, int? MedicineId = null);

/// <summary>Internal, server-side-only input to IMedicationInteractionChecker — never serialized over
/// HTTP directly. The caller (PrescriptionRepository) resolves MedicineId/ClientId into this fully-
/// resolved shape; the checker itself has no knowledge of how "existing medications" are modeled.</summary>
public record MedicationSafetyCheckRequest(
    int ClientId,
    string CandidateMedicineName,
    string? CandidateGenericName,
    IReadOnlyCollection<int> CandidateIngredientIds,
    IReadOnlyCollection<ExistingMedicationIngredient> ExistingMedicationIngredients,
    /// <summary>The catalogue row being prescribed, when there is one. Null for a free-text or
    /// compounded item, which can't be duplicate-therapy checked any more than it can be
    /// interaction checked.</summary>
    int? CandidateMedicineId = null);

/// <summary>Non-blocking, overridable drug-drug and drug-allergy safety check — the swap seam for a
/// future licensed clinical-decision-support feed, mirrored on IBillingCalculator's own style
/// (depends on AppDbContext directly plus one specific repository, not over-abstracted). Storage-
/// agnostic about "existing medications": the caller decides what counts as active and passes
/// resolved ingredient ids in, so this has no dependency on the Prescription entity shape.</summary>
public interface IMedicationInteractionChecker
{
    Task<MedicationSafetyCheckResultDto> CheckAsync(MedicationSafetyCheckRequest request);
}

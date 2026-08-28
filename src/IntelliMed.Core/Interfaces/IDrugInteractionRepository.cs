using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IDrugInteractionRepository : IRepository<DrugInteraction>
{
    new Task<DrugInteractionDto?> GetByIdAsync(int id);
    Task<IEnumerable<DrugInteractionDto>> GetAllAsync(int take = 200);
    Task<int> CreateAsync(CreateDrugInteractionDto dto);
    Task UpdateAsync(int id, UpdateDrugInteractionDto dto);

    /// <summary>Reference-data row count, active or not — used to distinguish "checked, found nothing"
    /// from "nothing loaded to check against" (see MedicationSafetyCheckResultDto.InteractionDatabaseEmpty).</summary>
    Task<int> CountAsync();

    /// <summary>Every active DrugInteraction where one side's ingredient is in `candidateIngredientIds`
    /// and the other side's is in `existingIngredientIds` — used by MedicationInteractionChecker.
    /// Symmetric: doesn't matter which side of a stored pair either id lands on.</summary>
    Task<List<DrugInteraction>> FindInteractionsAsync(
        IReadOnlyCollection<int> candidateIngredientIds, IReadOnlyCollection<int> existingIngredientIds);
}

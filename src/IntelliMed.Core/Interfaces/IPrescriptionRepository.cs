using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.Interfaces;

public interface IPrescriptionRepository : IRepository<Prescription>
{
    new Task<PrescriptionDto?> GetByIdAsync(int id);
    Task<IEnumerable<PrescriptionDto>> GetByClientIdAsync(int clientId, bool includeInactive = false);
    Task<int> CreateAsync(CreatePrescriptionDto dto);
    Task UpdateAsync(int id, UpdatePrescriptionDto dto);
    Task CeaseAsync(int id);
    Task CancelAsync(int id);

    /// <summary>Ingredient ids for a client's currently-Active, catalog-linked prescriptions, paired
    /// with each prescription's medicine name — the "existing medications" side of a safety check.
    /// Free-text (MedicineId == null) prescriptions contribute nothing, since there's no ingredient to
    /// resolve them to.</summary>
    Task<List<ExistingMedicationIngredient>> GetActiveIngredientsForClientAsync(int clientId);
}

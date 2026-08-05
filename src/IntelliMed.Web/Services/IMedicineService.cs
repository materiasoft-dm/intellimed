using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Web.Services;

public interface IMedicineService
{
    Task<List<MedicineDto>> SearchAsync(string query, MedicineSource? source = null);
    Task<MedicineImportResultDto?> ImportAsync(Stream fileStream, string fileName);
    Task<IngredientEnrichmentResultDto?> EnrichIngredientsAsync();
    Task<MedicineDto?> CreateAsync(CreateMedicineDto dto);
    Task<bool> UpdateAsync(int id, UpdateMedicineDto dto);
    Task<bool> DeactivateAsync(int id);
}

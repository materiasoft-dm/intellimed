using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IDrugInteractionService
{
    Task<List<DrugInteractionDto>> GetAllAsync();
    Task<bool> CreateAsync(CreateDrugInteractionDto dto);
    Task<bool> UpdateAsync(int id, UpdateDrugInteractionDto dto);
    Task<DrugInteractionImportResultDto?> ImportAsync(Stream fileStream, string fileName);
    Task<byte[]?> ExportCsvAsync();
}

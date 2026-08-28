using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

public interface IDrugInteractionImportService
{
    Task<DrugInteractionImportResultDto> ImportAsync(Stream csvStream);
}

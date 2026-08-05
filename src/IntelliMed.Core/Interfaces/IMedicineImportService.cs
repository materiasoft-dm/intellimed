using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

public interface IMedicineImportService
{
    Task<MedicineImportResultDto> ImportAsync(Stream csvStream);
}

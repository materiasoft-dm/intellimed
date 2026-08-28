using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IProgressNoteService
{
    Task<List<ProgressNoteDto>> GetByClientIdAsync(int clientId);
    Task<ProgressNoteDto?> GetByIdAsync(int clientId, int id);
    Task<int?> CreateAsync(CreateProgressNoteDto dto);
    Task<bool> UpdateAsync(int clientId, int id, UpdateProgressNoteDto dto);
    Task<bool> FinalizeAsync(int clientId, int id, FinalizeProgressNoteDto dto);
    Task<bool> AddAddendumAsync(int clientId, int id, AddProgressNoteAddendumDto dto);
    Task<bool> DeleteAsync(int clientId, int id);
}

using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;

namespace IntelliMed.Web.Services;

public interface IMedicineService
{
    Task<List<MedicineDto>> SearchAsync(string query, MedicineSource? source = null);
    Task<MedicineImportResultDto?> ImportAsync(Stream fileStream, string fileName);

    /// <summary>Backfills poisons schedule, ARTG ID, route and form from a TGA ARTG "Medicines"
    /// export. Returns null on failure; the caller shows a generic message.</summary>
    Task<ArtgEnrichmentResultDto?> EnrichFromArtgAsync(Stream fileStream, string fileName);
    Task<byte[]?> ExportCsvAsync();

    /// <summary>Starts a Sync job (assemble from every source, import directly, then auto-enrich
    /// ingredients) and polls it to completion, relaying each new log line to `progress` as it
    /// arrives. `onJobStarted` fires as soon as the job's id is known — before polling begins — so
    /// the caller can enable a Cancel button against that same job. Returns the final job state
    /// (check IsCancelled/Error on it) for anything that reaches a normal completion; returns null
    /// only for a request-level failure (e.g. not authorized); throws InvalidOperationException if
    /// the server loses the job mid-poll (e.g. restarted).</summary>
    Task<SyncProgressDto?> SyncFromSourcesAsync(IProgress<string> progress, Action<string> onJobStarted);
    Task<bool> CancelSyncAsync(string jobId);

    Task<MedicineDto?> CreateAsync(CreateMedicineDto dto);
    Task<bool> UpdateAsync(int id, UpdateMedicineDto dto);
    Task<bool> DeactivateAsync(int id);
}

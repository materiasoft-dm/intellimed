using IntelliMed.Core.DTOs;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// In-memory store for medicine sync job progress. Jobs are keyed by a GUID and
/// cleaned up after 30 minutes. Not persisted — restarting the API loses in-flight jobs
/// (and orphans their background Task, which keeps running until it hits its own
/// cancellation/completion — see MedicinesController.SyncFromSources).
/// </summary>
public static class SyncProgressStore
{
    public static readonly Dictionary<string, SyncJobState> Jobs = new();

    static SyncProgressStore()
    {
        // Periodic cleanup of old jobs
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                lock (Jobs)
                {
                    var cutoff = DateTime.UtcNow.AddMinutes(-30);
                    var expired = Jobs.Where(kv => kv.Value.CreatedAt < cutoff).Select(kv => kv.Key).ToList();
                    foreach (var key in expired)
                    {
                        Jobs[key].Cts.Dispose();
                        Jobs.Remove(key);
                    }
                }
            }
        });
    }
}

public class SyncJobState
{
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public bool IsComplete { get; set; }
    public bool IsCancelled { get; set; }
    public string Status { get; set; } = "Starting...";
    public List<string> Log { get; } = new();
    public string? Error { get; set; }
    public MedicineImportResultDto? ImportResult { get; set; }
    public IngredientEnrichmentResultDto? EnrichmentResult { get; set; }

    /// <summary>Lets the cancel endpoint stop this specific job's background work — checked before
    /// every retry attempt and passed into every HTTP call/delay in PbsApiMedicineSourceProvider, so
    /// cancelling during an 80s rate-limit backoff stops it immediately rather than waiting it out.</summary>
    public CancellationTokenSource Cts { get; } = new();
}

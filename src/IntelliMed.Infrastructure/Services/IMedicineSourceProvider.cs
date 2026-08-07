namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// A pluggable source of candidate medicine catalog rows for MedicineCsvAssemblyService. Each
/// implementation fetches from one external source and maps its results into the same row shape
/// MedicineCsvParser consumes, so assembled output can be imported through the existing pipeline
/// unchanged (whether piped straight into MedicineImportService by the Sync job, or written out via
/// MedicineRepository.ExportCsvAsync). Register additional implementations in ServiceExtensions —
/// MedicineCsvAssemblyService resolves every registered IMedicineSourceProvider automatically, so a
/// future AMT/PBS/ARTG provider (once one of those sources is actually cleared for use — see the plan
/// doc) is a one-line DI registration, not a rewrite of the aggregation logic.
/// </summary>
public interface IMedicineSourceProvider
{
    /// <summary>Shown in logs/errors if this source fails — keep it short and human-readable.</summary>
    string SourceName { get; }

    /// <summary>Optional fine-grained progress (page N/M, rate-limit waits, phase changes) for
    /// sources whose fetch takes long enough that silence would look like a hang — see
    /// PbsApiMedicineSourceProvider. Sources that complete quickly can ignore it. Implementations
    /// must let a cancelled cancellationToken surface as OperationCanceledException rather than
    /// swallowing it — MedicineCsvAssemblyService specifically distinguishes cancellation from an
    /// ordinary per-source failure.</summary>
    Task<IReadOnlyList<ParsedMedicineRow>> FetchAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

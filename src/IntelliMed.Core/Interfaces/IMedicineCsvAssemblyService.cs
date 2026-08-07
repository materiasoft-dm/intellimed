namespace IntelliMed.Core.Interfaces;

public interface IMedicineCsvAssemblyService
{
    /// <summary>Fetches from every registered medicine data source and returns the merged result as
    /// CSV text in MedicineImportService's format — the caller decides what to do with it (the Sync
    /// job pipes it straight into ImportAsync). Reports progress messages via <paramref name="progress"/>
    /// and honours <paramref name="cancellationToken"/> — a cancelled token surfaces as
    /// OperationCanceledException, not a swallowed per-source failure.</summary>
    Task<string> AssembleCsvAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

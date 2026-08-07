using System.Text;
using IntelliMed.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Aggregates every registered IMedicineSourceProvider into one CSV in MedicineCsvParser's format.
/// Pure aggregation only — doesn't touch the database itself. The Sync job (MedicinesController)
/// pipes the result straight into MedicineImportService; MedicineRepository.ExportCsvAsync reuses
/// the CSV-writing helpers below (Header/ToCsv, internal for that reason) for the unrelated "export
/// what's already in the database" feature, which never calls this class at all.
/// </summary>
public class MedicineCsvAssemblyService : IMedicineCsvAssemblyService
{
    internal static readonly string[] Header =
    [
        "Name", "GenericName", "Strength", "Form", "PackSize", "Manufacturer", "ArtgId",
        "IsPbsListed", "IsRpbsListed", "RestrictionLevel", "Schedule", "ActiveIngredients",
        "PbsItemCode", "AmtConceptId", "AtcCode"
    ];

    private readonly IEnumerable<IMedicineSourceProvider> _providers;
    private readonly ILogger<MedicineCsvAssemblyService> _logger;

    public MedicineCsvAssemblyService(IEnumerable<IMedicineSourceProvider> providers, ILogger<MedicineCsvAssemblyService> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public async Task<string> AssembleCsvAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        // First source to report a given key wins — providers are registered in priority order
        // (most authoritative first) once more than one exists, so an earlier source's row is kept
        // over a later, less-trusted one rather than the other way around. Keyed by Name+Strength+
        // Form+PackSize (matching MedicineImportService.MatchKey) rather than Name alone — a
        // Name-only key was fine back when Wikidata was the only source (one row per generic name),
        // but PBS routinely has many items sharing a brand name at different strengths/forms/pack
        // sizes; a Name-only key was found to collapse a real 14,783-row PBS pull down to 3,659,
        // silently discarding distinct medicines. PackSize was added after Strength+Form alone still
        // let two genuinely different pack sizes of the same brand/strength/form collide — the PBS
        // API Data Dictionary confirms pack quantity is one of the dimensions that gets its own item
        // code. Matching the import service's own key means assembly-time dedup collapses exactly
        // the rows the importer would have upserted together anyway, no more.
        var byKey = new Dictionary<string, ParsedMedicineRow>(StringComparer.OrdinalIgnoreCase);

        var providerList = _providers.ToList();
        progress?.Report($"Starting assembly from {providerList.Count} source(s)...");

        foreach (var provider in providerList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<ParsedMedicineRow> rows;
            try
            {
                progress?.Report($"Fetching from {provider.SourceName}...");
                rows = await provider.FetchAsync(progress, cancellationToken);
                progress?.Report($"{provider.SourceName}: received {rows.Count} row(s)");
            }
            catch (OperationCanceledException)
            {
                // A user-requested cancel, not a source failure — must propagate, not be treated as
                // "this source is down, try the next one" like the catch-all below.
                throw;
            }
            catch (Exception ex)
            {
                // One source being unreachable shouldn't block the others — this assembles whatever
                // it can rather than failing the whole run.
                _logger.LogWarning(ex, "Medicine source '{SourceName}' failed, skipping it", provider.SourceName);
                progress?.Report($"{provider.SourceName}: FAILED — {ex.Message}");
                continue;
            }

            var added = 0;
            foreach (var row in rows)
            {
                if (byKey.TryAdd(MatchKey(row.Name, row.Strength, row.Form, row.PackSize), row))
                    added++;
            }
            progress?.Report($"{provider.SourceName}: {added} new row(s) added (total: {byKey.Count})");
        }

        _logger.LogInformation("Assembled {Count} medicine row(s) from {ProviderCount} source(s)", byKey.Count, providerList.Count);
        progress?.Report($"Assembly complete — {byKey.Count} total unique medicine(s). Generating CSV...");

        return ToCsv(byKey.Values.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase));
    }

    // Must stay identical to MedicineImportService.MatchKey — see the comment above.
    private static string MatchKey(string name, string? strength, string? form, string? packSize) =>
        $"{name.Trim()}|{strength?.Trim()}|{form?.Trim()}|{packSize?.Trim()}".ToLowerInvariant();

    internal static string ToCsv(IEnumerable<ParsedMedicineRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Header));

        foreach (var row in rows)
        {
            string[] fields =
            [
                row.Name,
                row.GenericName ?? string.Empty,
                row.Strength ?? string.Empty,
                row.Form ?? string.Empty,
                row.PackSize ?? string.Empty,
                row.Manufacturer ?? string.Empty,
                row.ArtgId ?? string.Empty,
                row.IsPbsListed.ToString(),
                row.IsRpbsListed.ToString(),
                row.RestrictionLevel?.ToString() ?? string.Empty,
                row.Schedule?.ToString() ?? string.Empty,
                string.Join(";", row.ActiveIngredients),
                row.PbsItemCode ?? string.Empty,
                row.AmtConceptId ?? string.Empty,
                row.AtcCode ?? string.Empty
            ];
            sb.AppendLine(string.Join(",", fields.Select(CsvEscape)));
        }

        return sb.ToString();
    }

    private static string CsvEscape(string field) =>
        field.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
}

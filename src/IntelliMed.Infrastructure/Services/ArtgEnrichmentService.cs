using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Backfills catalogue rows from a TGA ARTG Medicines export.
///
/// This exists because PBS — the catalogue's main source — publishes no poisons scheduling at all,
/// which left `Medicine.Schedule` null for every synced row and silently disabled the prescribing
/// wizard's Schedule 8 gate (MEDICINE_DATA_ENRICHMENT.md §1). ARTG carries the schedule, so this
/// closes that gap without a separate SUSMP integration.
///
/// It is an *enricher*, not an IMedicineSourceProvider: it adds fields to medicines already in the
/// catalogue and never creates new ones. Growing the catalogue from ARTG is a separate, harder
/// problem — ARTG has no distinct strength column, so its rows don't key cleanly against
/// Name+Strength+Form+PackSize.
/// </summary>
public class ArtgEnrichmentService : IArtgEnrichmentService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ArtgEnrichmentService> _logger;

    public ArtgEnrichmentService(AppDbContext context, ILogger<ArtgEnrichmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Brand key for matching. ARTG product names embed everything —
    /// "GABAPENTIN-WGR gabapentin 600 mg film coated tablets blister pack" — while a PBS-sourced
    /// catalogue name is the brand alone ("GABAPENTIN-WGR"), so the leading whitespace-delimited
    /// token is the piece both sides share.
    ///
    /// Measured at 86% coverage of the current catalogue. The misses are mostly generic-name rows
    /// from non-PBS sources, which have no brand to match on by construction.
    /// </summary>
    private static string BrandKey(string name)
    {
        var trimmed = name.Trim();
        var space = trimmed.IndexOf(' ');
        return (space > 0 ? trimmed[..space] : trimmed).ToUpperInvariant();
    }

    private static int Strictness(MedicineSchedule s) => s switch
    {
        MedicineSchedule.S8 => 4,
        MedicineSchedule.S4 => 3,
        MedicineSchedule.S3 => 2,
        MedicineSchedule.S2 => 1,
        _ => 0   // Unscheduled
    };

    public async Task<ArtgEnrichmentResultDto> EnrichAsync(TextReader csv, CancellationToken cancellationToken = default)
    {
        var result = new ArtgEnrichmentResultDto { CompletedAt = DateTime.UtcNow };

        // Collapse ARTG to one entry per brand. A brand routinely has many registrations (strengths,
        // pack sizes, sponsors); the strictest schedule across them wins, for the same
        // safety-conservative reason ArtgCsvParser.MapSchedule prefers the strictest within a cell.
        var byBrand = new Dictionary<string, ArtgMedicineRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ArtgCsvParser.Parse(csv))
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.ArtgRows++;

            var key = BrandKey(row.ProductName);
            if (key.Length == 0) continue;

            if (!byBrand.TryGetValue(key, out var existing))
            {
                byBrand[key] = row;
                continue;
            }

            // Keep the strictest schedule, but let a row with data fill gaps the incumbent left.
            var better = row.Schedule.HasValue &&
                         (!existing.Schedule.HasValue || Strictness(row.Schedule.Value) > Strictness(existing.Schedule.Value));

            byBrand[key] = new ArtgMedicineRow(
                existing.ArtgId,
                existing.ProductName,
                better ? row.Schedule : existing.Schedule ?? row.Schedule,
                existing.DosageForm ?? row.DosageForm,
                existing.Route ?? row.Route);
        }

        _logger.LogInformation("ARTG enrichment: parsed {Rows} row(s) into {Brands} brand(s)",
            result.ArtgRows, byBrand.Count);

        var medicines = await _context.Medicines.Where(m => m.IsActive).ToListAsync(cancellationToken);
        result.CatalogueRows = medicines.Count;

        foreach (var medicine in medicines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!byBrand.TryGetValue(BrandKey(medicine.Name), out var artg))
            {
                result.Unmatched++;
                continue;
            }

            result.Matched++;
            var changed = false;

            // Fill-only, never overwrite: a manually curated schedule or a value from a more
            // authoritative source outranks a brand-level ARTG match.
            if (medicine.Schedule == null && artg.Schedule.HasValue)
            {
                medicine.Schedule = artg.Schedule;
                result.ScheduleFilled++;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(medicine.ArtgId))
            {
                medicine.ArtgId = artg.ArtgId;
                result.ArtgIdFilled++;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(medicine.Route) && artg.Route != null)
            {
                medicine.Route = artg.Route;
                result.RouteFilled++;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(medicine.Form) && artg.DosageForm != null)
            {
                medicine.Form = artg.DosageForm;
                result.FormFilled++;
                changed = true;
            }

            if (!changed) result.AlreadyComplete++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        result.ScheduleBreakdown = await _context.Medicines
            .Where(m => m.IsActive && m.Schedule != null)
            .GroupBy(m => m.Schedule!.Value)
            .Select(g => new { Schedule = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Schedule.ToString(), x => x.Count, cancellationToken);

        result.Message =
            $"Matched {result.Matched} of {result.CatalogueRows} catalogue medicines; " +
            $"filled {result.ScheduleFilled} schedule(s), {result.ArtgIdFilled} ARTG ID(s), " +
            $"{result.RouteFilled} route(s), {result.FormFilled} form(s).";

        _logger.LogInformation("ARTG enrichment complete. {Message}", result.Message);
        return result;
    }
}

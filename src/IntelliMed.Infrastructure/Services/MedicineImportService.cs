using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Imports Medicine/ActiveIngredient reference data from the CSV format documented in
/// MedicineCsvParser. Deliberately source-agnostic: the CSV is produced by
/// MedicineCsvAssemblyService from any number of registered IMedicineSourceProvider implementations
/// (Wikidata today, PBS API Public v3 added 2026-08 — see README "Medicine data sources & licensing"
/// for the exact terms of each). PBS data is stored verbatim per its redistribution licence
/// (retain copyright, do not modify); TGA/ARTG is not currently ingested. New sources plug in as
/// additional IMedicineSourceProvider registrations — no changes needed here. A PBS item missing
/// from a full-size PBS batch is treated as delisted and deactivated (never hard-deleted) — see the
/// delist-check block below for the exact safety gating.
/// </summary>
public class MedicineImportService : IMedicineImportService
{
    private readonly AppDbContext _context;

    public MedicineImportService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MedicineImportResultDto> ImportAsync(Stream csvStream)
    {
        using var reader = new StreamReader(csvStream);
        var csvText = await reader.ReadToEndAsync();
        var rows = MedicineCsvParser.Parse(csvText);

        var now = DateTime.UtcNow;
        int added = 0, updated = 0, unmatched = 0;

        var existingMedicines = await _context.Medicines
            .Include(m => m.Ingredients).ThenInclude(mi => mi.ActiveIngredient)
            .ToListAsync();
        var existingByKey = existingMedicines.ToDictionary(m => MatchKey(m.Name, m.Strength, m.Form, m.PackSize), StringComparer.OrdinalIgnoreCase);

        var ingredientsByName = (await _context.ActiveIngredients.ToListAsync())
            .ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var key = MatchKey(row.Name, row.Strength, row.Form, row.PackSize);
            if (!existingByKey.TryGetValue(key, out var medicine))
            {
                // Source is set here, once, and never touched again below — a row that started
                // Manual (or was matched from a prior sync) keeps its provenance even if a later
                // import updates its other fields via the same name/strength/form key.
                medicine = new Medicine { CreatedAt = now, Source = MedicineSource.Synced };
                _context.Medicines.Add(medicine);
                existingByKey[key] = medicine;
                added++;
            }
            else
            {
                updated++;
            }

            medicine.Name = row.Name;
            medicine.GenericName = row.GenericName;
            medicine.Strength = row.Strength;
            medicine.Form = row.Form;
            medicine.PackSize = row.PackSize;
            medicine.Manufacturer = row.Manufacturer;
            medicine.ArtgId = row.ArtgId;
            medicine.IsPbsListed = row.IsPbsListed;
            medicine.IsRpbsListed = row.IsRpbsListed;
            medicine.RestrictionLevel = row.RestrictionLevel;
            medicine.Schedule = row.Schedule;
            // Provenance IDs — only overwrite when the source actually supplied one, so a later
            // import that doesn't carry these (e.g. a hand-edited CSV) doesn't blank out a value
            // a prior PBS sync had set. PBS data is stored verbatim per its redistribution licence.
            if (!string.IsNullOrWhiteSpace(row.PbsItemCode)) medicine.PbsItemCode = row.PbsItemCode;
            if (!string.IsNullOrWhiteSpace(row.AmtConceptId)) medicine.AmtConceptId = row.AmtConceptId;
            if (!string.IsNullOrWhiteSpace(row.AtcCode)) medicine.AtcCode = row.AtcCode;

            // Prescribing limits and classification — same "only overwrite when supplied" rule as
            // the provenance IDs above, so a CSV import without these columns can't wipe values a
            // PBS sync established.
            if (row.MaxRepeats.HasValue) medicine.MaxRepeats = row.MaxRepeats;
            if (row.MaxQuantity.HasValue) medicine.MaxQuantity = row.MaxQuantity;
            if (!string.IsNullOrWhiteSpace(row.Route)) medicine.Route = row.Route;
            if (row.IsSection100) medicine.IsSection100 = true;
            if (!string.IsNullOrWhiteSpace(row.TherapeuticGroupId)) medicine.TherapeuticGroupId = row.TherapeuticGroupId;
            if (!string.IsNullOrWhiteSpace(row.TherapeuticGroupTitle)) medicine.TherapeuticGroupTitle = row.TherapeuticGroupTitle;
            medicine.IsActive = true;
            medicine.LastSyncedAt = now;

            foreach (var ingredientName in row.ActiveIngredients)
            {
                if (!ingredientsByName.TryGetValue(ingredientName, out var ingredient))
                {
                    ingredient = new ActiveIngredient { Name = ingredientName, IsAmtConfirmed = false };
                    _context.ActiveIngredients.Add(ingredient);
                    ingredientsByName[ingredientName] = ingredient;
                    unmatched++;
                }

                var alreadyLinked = medicine.Ingredients.Any(mi =>
                    mi.ActiveIngredient != null &&
                    string.Equals(mi.ActiveIngredient.Name, ingredientName, StringComparison.OrdinalIgnoreCase));
                if (!alreadyLinked)
                {
                    medicine.Ingredients.Add(new MedicineIngredient { Medicine = medicine, ActiveIngredient = ingredient });
                }
            }
        }

        // PBS items that no longer appear in a full PBS pull have been delisted from the schedule —
        // deactivate them rather than leaving a stale, still-active record behind indefinitely.
        // Scoped strictly to PbsItemCode (only ever set by the PBS provider, and PBS's own stable
        // identifier for that exact prescribing rule — never touched by manual create/update) and
        // gated on this batch actually looking like a real PBS pull, so a small hand-edited or
        // non-PBS CSV import can never accidentally deactivate the whole PBS-derived catalog.
        // Mirrors BillingItemSyncService's "no longer in this quarter's feed = retired" rule.
        const int MinPbsRowsForDelistCheck = 1000;
        var pbsCodesInBatch = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.PbsItemCode))
            .Select(r => r.PbsItemCode!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int deactivated = 0;
        if (pbsCodesInBatch.Count >= MinPbsRowsForDelistCheck)
        {
            foreach (var medicine in existingMedicines)
            {
                if (medicine.IsActive &&
                    !string.IsNullOrWhiteSpace(medicine.PbsItemCode) &&
                    !pbsCodesInBatch.Contains(medicine.PbsItemCode))
                {
                    medicine.IsActive = false;
                    medicine.LastSyncedAt = now;
                    deactivated++;
                }
            }
        }

        await _context.SaveChangesAsync();

        return new MedicineImportResultDto
        {
            Added = added,
            Updated = updated,
            Unmatched = unmatched,
            Deactivated = deactivated,
            ImportedAt = now,
            Message = $"{added} added, {updated} updated, {deactivated} deactivated (no longer PBS-listed), " +
                      $"{unmatched} new ingredient(s) auto-created (not AMT-confirmed)."
        };
    }

    private static string MatchKey(string name, string? strength, string? form, string? packSize) =>
        $"{name.Trim()}|{strength?.Trim()}|{form?.Trim()}|{packSize?.Trim()}".ToLowerInvariant();
}

using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Imports Medicine/ActiveIngredient reference data from the CSV format documented in
/// MedicineCsvParser. Deliberately source-agnostic: PBS's copyright terms restrict reproduction to
/// personal reference use, and TGA/ARTG's terms are believed similarly restrictive, so this is
/// currently pointed at a small self-compiled starter list (medicines_starter.csv at the repo root)
/// rather than a copy of either published database. Once a source is actually cleared for this use
/// (an AMT affiliate sub-license, or written permission from TGA/Health), map that export into this
/// same CSV shape — no changes needed here.
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
        var existingByKey = existingMedicines.ToDictionary(m => MatchKey(m.Name, m.Strength, m.Form), StringComparer.OrdinalIgnoreCase);

        var ingredientsByName = (await _context.ActiveIngredients.ToListAsync())
            .ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var key = MatchKey(row.Name, row.Strength, row.Form);
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
            medicine.Manufacturer = row.Manufacturer;
            medicine.ArtgId = row.ArtgId;
            medicine.IsPbsListed = row.IsPbsListed;
            medicine.Schedule = row.Schedule;
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

        await _context.SaveChangesAsync();

        return new MedicineImportResultDto
        {
            Added = added,
            Updated = updated,
            Unmatched = unmatched,
            ImportedAt = now,
            Message = $"{added} added, {updated} updated, {unmatched} new ingredient(s) auto-created (not AMT-confirmed)."
        };
    }

    private static string MatchKey(string name, string? strength, string? form) =>
        $"{name.Trim()}|{strength?.Trim()}|{form?.Trim()}".ToLowerInvariant();
}

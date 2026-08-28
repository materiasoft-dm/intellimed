using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Imports DrugInteraction reference data from the CSV format documented in
/// DrugInteractionCsvParser. Ships with no bundled dataset — the obvious open source (DDInter) is
/// CC-BY-NC and needs explicit Oxford University Press permission before it can be redistributed here
/// (see the plan doc). Ingredient names are matched against ActiveIngredient by exact case-insensitive
/// name, auto-creating (IsAmtConfirmed = false) when not found — same convention as
/// MedicineImportService's ingredient linking.
/// </summary>
public class DrugInteractionImportService : IDrugInteractionImportService
{
    private readonly AppDbContext _context;

    public DrugInteractionImportService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DrugInteractionImportResultDto> ImportAsync(Stream csvStream)
    {
        using var reader = new StreamReader(csvStream);
        var csvText = await reader.ReadToEndAsync();
        var rows = DrugInteractionCsvParser.Parse(csvText);

        var now = DateTime.UtcNow;
        var ingredientsAutoCreated = 0;

        var ingredientsByName = (await _context.ActiveIngredients.ToListAsync())
            .ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

        // First pass: make sure every referenced ingredient exists with a real, saved Id — needed
        // before canonical A<B ordering (below) can be computed, since a brand-new entity has no Id
        // until SaveChanges runs.
        foreach (var row in rows)
        {
            EnsureIngredient(row.IngredientAName, ingredientsByName, ref ingredientsAutoCreated);
            EnsureIngredient(row.IngredientBName, ingredientsByName, ref ingredientsAutoCreated);
        }
        if (ingredientsAutoCreated > 0) await _context.SaveChangesAsync();

        var existingByKey = (await _context.DrugInteractions.ToListAsync())
            .ToDictionary(d => (d.IngredientAId, d.IngredientBId));

        int added = 0, updated = 0;
        foreach (var row in rows)
        {
            var rawIdA = ingredientsByName[row.IngredientAName].Id;
            var rawIdB = ingredientsByName[row.IngredientBName].Id;
            var idA = Math.Min(rawIdA, rawIdB);
            var idB = Math.Max(rawIdA, rawIdB);

            if (!existingByKey.TryGetValue((idA, idB), out var interaction))
            {
                interaction = new DrugInteraction { IngredientAId = idA, IngredientBId = idB };
                _context.DrugInteractions.Add(interaction);
                existingByKey[(idA, idB)] = interaction;
                added++;
            }
            else
            {
                updated++;
            }

            interaction.Severity = row.Severity;
            interaction.Description = row.Description;
            interaction.SourceDataset = row.SourceDataset;
            interaction.IsActive = true;
        }

        await _context.SaveChangesAsync();

        return new DrugInteractionImportResultDto
        {
            Added = added,
            Updated = updated,
            IngredientsAutoCreated = ingredientsAutoCreated,
            ImportedAt = now,
            Message = $"{added} added, {updated} updated, {ingredientsAutoCreated} new ingredient(s) auto-created (not AMT-confirmed)."
        };
    }

    private void EnsureIngredient(string name, Dictionary<string, ActiveIngredient> byName, ref int autoCreatedCount)
    {
        if (byName.ContainsKey(name)) return;

        var created = new ActiveIngredient { Name = name, IsAmtConfirmed = false };
        _context.ActiveIngredients.Add(created);
        byName[name] = created;
        autoCreatedCount++;
    }
}

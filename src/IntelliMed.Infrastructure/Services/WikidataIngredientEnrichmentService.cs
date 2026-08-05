using System.Text.Json;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Enriches ActiveIngredient rows with an ATC code from Wikidata (CC0-licensed — see
/// Wikidata:Licensing — so safe for commercial reuse unlike PBS/ARTG). Verified against the live API
/// before writing this: entity search via wbsearchentities, ATC code at claims.P267 on the matched
/// entity (https://www.wikidata.org/wiki/Property:P267). Identification-layer cross-reference only,
/// not a clinical authority — deliberately never touches AmtSubstanceId/IsAmtConfirmed, which stay
/// reserved for an actual AMT match once that's registered for.
/// </summary>
public class WikidataIngredientEnrichmentService : IIngredientEnrichmentService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;

    public WikidataIngredientEnrichmentService(HttpClient httpClient, AppDbContext context)
    {
        _httpClient = httpClient;
        _context = context;
    }

    public async Task<IngredientEnrichmentResultDto> EnrichFromWikidataAsync()
    {
        // Re-attempts previous no-matches too (WikidataId stays null either way) — cheap, and Wikidata
        // coverage grows over time, so a name that missed before may resolve on a later run.
        var candidates = await _context.ActiveIngredients
            .Where(i => i.WikidataId == null)
            .ToListAsync();

        int enriched = 0, notFound = 0;

        foreach (var ingredient in candidates)
        {
            var match = await FindMatchAsync(ingredient.Name);
            if (match == null)
            {
                notFound++;
            }
            else
            {
                ingredient.WikidataId = match.Value.QId;
                ingredient.AtcCode = match.Value.AtcCode;
                enriched++;
            }

            // Wikimedia API etiquette: don't hammer it with a tight bulk loop.
            await Task.Delay(200);
        }

        await _context.SaveChangesAsync();

        return new IngredientEnrichmentResultDto
        {
            Enriched = enriched,
            NotFound = notFound,
            TotalProcessed = candidates.Count,
            CompletedAt = DateTime.UtcNow
        };
    }

    private async Task<(string QId, string? AtcCode)?> FindMatchAsync(string ingredientName)
    {
        var searchUrl = "https://www.wikidata.org/w/api.php?action=wbsearchentities" +
            $"&search={Uri.EscapeDataString(ingredientName)}&language=en&format=json&limit=5&type=item";

        using var searchResponse = await _httpClient.GetAsync(searchUrl);
        if (!searchResponse.IsSuccessStatusCode) return null;

        using var searchDoc = JsonDocument.Parse(await searchResponse.Content.ReadAsStringAsync());
        if (!searchDoc.RootElement.TryGetProperty("search", out var results) || results.GetArrayLength() == 0)
            return null;

        // Scan the top few results for an exact (case-insensitive) label match rather than trusting
        // rank 0 — Wikidata sometimes ranks a more specific chemical-variant entity (e.g.
        // "rac-warfarin") above the general drug entity ("warfarin") that's what we actually want.
        // Still never accepts a fuzzy match like "amoxicillin allergy" for "Amoxicillin" — every
        // candidate is held to the same exact-label bar, just checked across more than one result.
        string? qId = null;
        foreach (var candidate in results.EnumerateArray())
        {
            var label = candidate.TryGetProperty("display", out var display) &&
                        display.TryGetProperty("label", out var labelEl) &&
                        labelEl.TryGetProperty("value", out var labelValueEl)
                ? labelValueEl.GetString()
                : null;
            if (!string.Equals(label, ingredientName, StringComparison.OrdinalIgnoreCase)) continue;

            qId = candidate.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            break;
        }
        if (string.IsNullOrEmpty(qId)) return null;

        var entityUrl = $"https://www.wikidata.org/wiki/Special:EntityData/{qId}.json";
        using var entityResponse = await _httpClient.GetAsync(entityUrl);
        if (!entityResponse.IsSuccessStatusCode) return (qId, null);

        using var entityDoc = JsonDocument.Parse(await entityResponse.Content.ReadAsStringAsync());
        string? atcCode = null;
        if (entityDoc.RootElement.TryGetProperty("entities", out var entities) &&
            entities.TryGetProperty(qId, out var entity) &&
            entity.TryGetProperty("claims", out var claims) &&
            claims.TryGetProperty("P267", out var atcClaims) &&
            atcClaims.GetArrayLength() > 0 &&
            atcClaims[0].TryGetProperty("mainsnak", out var mainsnak) &&
            mainsnak.TryGetProperty("datavalue", out var datavalue) &&
            datavalue.TryGetProperty("value", out var valueEl) &&
            valueEl.ValueKind == JsonValueKind.String)
        {
            atcCode = valueEl.GetString();
        }

        return (qId, atcCode);
    }
}

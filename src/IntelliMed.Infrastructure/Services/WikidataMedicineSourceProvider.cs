using System.Text.Json;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Discovers candidate medicine catalog rows from Wikidata (CC0-licensed — see
/// WikidataIngredientEnrichmentService for the same licensing note, safe for commercial reuse unlike
/// PBS/ARTG). Queries the public SPARQL endpoint for entities classified as a medication (Q12140) that
/// carry a substance-level ATC code (P267, filtered to the standard 7-character length so
/// pharmacological-group codes like "C05" don't come through as if they were a specific drug).
/// Verified directly against https://query.wikidata.org/sparql before writing this — 254 results at
/// time of writing, real medicine names paired with real ATC codes, no pagination needed.
///
/// Wikidata models substances/products in general, not Australian-specific brands or regulatory
/// status, and doesn't reliably distinguish a trade name from a generic/INN name either — so every row
/// comes back with only Name and ActiveIngredients populated; GenericName/Strength/Form/Manufacturer/
/// ArtgId/IsPbsListed/IsRpbsListed/RestrictionLevel/Schedule are left at their "unknown" defaults, same
/// as any other unconfirmed row in this catalog. This is an identification-layer source, meant to be
/// reviewed before import, not a clinical or regulatory authority.
/// </summary>
public class WikidataMedicineSourceProvider : IMedicineSourceProvider
{
    private readonly HttpClient _httpClient;

    public string SourceName => "Wikidata";

    public WikidataMedicineSourceProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ParsedMedicineRow>> FetchAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT DISTINCT ?item ?itemLabel ?atc WHERE {
              ?item wdt:P31/wdt:P279* wd:Q12140 .
              ?item wdt:P267 ?atc .
              ?item rdfs:label ?itemLabel .
              FILTER(LANG(?itemLabel) = "en")
              FILTER(STRLEN(?atc) = 7)
            } ORDER BY ?itemLabel LIMIT 2000
            """;

        var url = "https://query.wikidata.org/sparql?query=" + Uri.EscapeDataString(query);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/sparql-results+json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return Array.Empty<ParsedMedicineRow>();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!doc.RootElement.TryGetProperty("results", out var results) ||
            !results.TryGetProperty("bindings", out var bindings))
            return Array.Empty<ParsedMedicineRow>();

        var rows = new List<ParsedMedicineRow>();
        foreach (var binding in bindings.EnumerateArray())
        {
            var name = binding.TryGetProperty("itemLabel", out var labelEl) &&
                       labelEl.TryGetProperty("value", out var valueEl)
                ? valueEl.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Wikidata's convention for combination products is a "/"-separated label (e.g.
            // "vildagliptin / metformin") — split it the same way Panadeine's starter-CSV row does
            // for ActiveIngredients, so the interaction-checking layer sees each substance
            // individually rather than one opaque combo string.
            var ingredients = name.Contains('/')
                ? name.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [name];

            rows.Add(new ParsedMedicineRow(
                Name: name,
                GenericName: null,
                Strength: null,
                Form: null,
                PackSize: null,
                Manufacturer: null,
                ArtgId: null,
                IsPbsListed: false,
                IsRpbsListed: false,
                RestrictionLevel: null,
                Schedule: null,
                ActiveIngredients: ingredients));
        }
        return rows;
    }
}

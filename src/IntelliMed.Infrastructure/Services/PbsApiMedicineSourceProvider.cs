using System.Text.Json;
using System.Text.RegularExpressions;
using IntelliMed.Core.Entities;
using Microsoft.Extensions.Logging;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Discovers medicine catalog rows from the PBS API Public v3 (data-api.health.gov.au/pbs/api/v3/).
/// Uses the public subscription key shown on the API portal — no registration needed for low-volume
/// use. Every response carries a Commonwealth of Australia copyright notice that grants permission to
/// use and redistribute provided (1) the copyright statement is retained and (2) the content is not
/// modified. Accordingly, this provider stores PBS brand/generic/form fields verbatim and surfaces the
/// copyright notice for display in the Medicines admin UI. Strength is the one field split out of the
/// verbatim li_form text (see SplitFormAndStrength) — that's a presentational parse, not a content
/// change, so it doesn't run afoul of "do not modify". PackSize comes straight from the item's own
/// structured pack_size field (unlike MIMS, PBS doesn't need any text-parsing for this) and is part
/// of MedicineCsvAssemblyService/MedicineImportService's identity key, not just a display detail —
/// PBS assigns a distinct item code per pack quantity, so leaving it out of the key silently collapses
/// genuinely different medicines.
///
/// Data shape: calls /items?get_latest_schedule_only=true (paginated, ~30 pages / ~15k items), then
/// joins /item-atc-relationships for ATC codes (keyed by pbs_code, ~14 pages / ~7k rows), /schedules
/// for schedule names, and /organisations for manufacturer names (joined on organisation_id, which is
/// present directly on each item — manufacturer_code is a separate short alpha code, not usable as a
/// numeric join key). AMT concept IDs are NOT fetched here — the /amt-items endpoint has ~60k rows
/// (121 pages) and the PBS API rate limit makes a full pull impractical. AMT codes can be looked up on
/// demand via a separate per-item API call if needed.
///
/// Restriction level comes from benefit_type_code, mapped per the official PBS API Data Dictionary
/// (v3.7.8, "Four levels of restriction categorisation"): U=Unrestricted, R=Restricted,
/// A=Authority Required, S=Authority Required (Streamlined) — S collapses into AuthorityRequired,
/// consistent with PbsRestrictionLevel's existing doc comment.
///
/// Rate limiting: the PBS API allows ~5 requests/second before returning 429. We use 2.5 s between
/// requests with exponential backoff (10–80 s) on 429s. A full pull takes ~10–15 minutes. Item/ATC/
/// schedule page failures (after exhausting retries) throw, so a mid-pull failure surfaces as a
/// visible "this source failed" rather than silently returning a truncated-but-reported-as-complete
/// dataset; organisation lookups are enrichment-only and fail soft (a batch failure just leaves those
/// manufacturers blank rather than aborting an otherwise-successful ~15k-item pull).
///
/// Progress reporting: every internal page/batch fetch and every rate-limit retry reports through
/// the IProgress passed to FetchAsync — without this, the ~10-15 minute pull looks frozen in the
/// admin UI, since MedicineCsvAssemblyService only sees "Fetching from PBS..." and then silence
/// until the whole thing finishes. Reporting per-page (not just per-phase) keeps the modal visibly
/// moving even during a run of 429s, and names the wait explicitly ("rate limited — waiting Ns")
/// instead of leaving the user to guess whether it's hung.
///
/// Cancellation: the token is checked before every retry attempt and passed into every HTTP call and
/// Task.Delay (including the multi-second rate-limit backoffs) — cancelling while sitting in an 80s
/// backoff wait actually stops it immediately rather than waiting the delay out first.
/// </summary>
public class PbsApiMedicineSourceProvider : IMedicineSourceProvider
{
    /// <summary>Exact copyright notice from every PBS API response — must be retained with any
    /// redistributed data per the licence terms.</summary>
    public const string CopyrightNotice =
        "Copyright (c) 2024 Department of Health, Commonwealth of Australia. " +
        "Permission is granted to use and redistribute this content, as long as all copyright " +
        "statements are retained. Permission is not granted to modify this content. " +
        "For further copyright information: https://www.health.gov.au/using-our-websites/copyright";

    private const string BaseUrl = "https://data-api.health.gov.au/pbs/api/v3/";
    private const int PageSize = 500;
    /// <summary>PBS API rate-limits aggressively — 2.5 s between requests keeps us safely under
    /// the threshold for the ~50 requests a full pull needs (items + ATC + schedules + orgs).</summary>
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(2500);

    private readonly HttpClient _httpClient;
    private readonly ILogger<PbsApiMedicineSourceProvider> _logger;

    public string SourceName => "PBS API Public v3";

    public PbsApiMedicineSourceProvider(HttpClient httpClient, ILogger<PbsApiMedicineSourceProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ParsedMedicineRow>> FetchAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        // Step 1: pull ALL items (current schedule only — get_latest_schedule_only=true cuts the
        // ~190k total down to the active schedule, ~15k items as of 2026).
        _logger.LogInformation("PBS: fetching items (current schedule)...");
        progress?.Report("PBS: fetching items (current schedule)...");
        var items = await FetchAllPagesAsync("items?get_latest_schedule_only=true", "items", progress, cancellationToken);
        if (items.Count == 0) return Array.Empty<ParsedMedicineRow>();

        _logger.LogInformation("PBS: fetched {Count} items, building lookup maps", items.Count);
        progress?.Report($"PBS: fetched {items.Count} item(s), building lookup maps...");

        // Step 2: build lookup maps from the items we fetched.
        var scheduleCodes = items.Select(i => GetField(i, "schedule_code")).Where(c => c > 0).Distinct().ToHashSet();

        // Step 3: fetch ATC relationships by schedule_code (paginated — ~7k rows for current schedule).
        // ATC relationships use pbs_code, not li_item_id.
        _logger.LogInformation("PBS: fetching ATC relationships...");
        progress?.Report("PBS: fetching ATC relationships...");
        var atcRels = await FetchAllForScheduleCodeAsync("item-atc-relationships", scheduleCodes, "ATC relationships", progress, cancellationToken);
        var atcByPbsCode = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in atcRels)
        {
            var pbs = GetString(rel, "pbs_code");
            var atc = GetString(rel, "atc_code");
            if (!string.IsNullOrWhiteSpace(pbs) && !string.IsNullOrWhiteSpace(atc))
                atcByPbsCode[pbs] = atc;
        }
        _logger.LogInformation("PBS: fetched {Count} ATC relationship(s), {Mapped} mapped to pbs_code",
            atcRels.Count, atcByPbsCode.Count);
        progress?.Report($"PBS: fetched {atcRels.Count} ATC relationship(s)");

        // Step 4: fetch schedules (for schedule name -> MedicineSchedule mapping).
        _logger.LogInformation("PBS: fetching schedules...");
        progress?.Report("PBS: fetching schedules...");
        var schedules = await FetchAllForScheduleCodeAsync("schedules", scheduleCodes, "schedules", progress, cancellationToken);
        var scheduleMap = new Dictionary<int, string>();
        foreach (var s in schedules)
        {
            var code = GetField(s, "schedule_code");
            var name = GetString(s, "schedule_name");
            if (code > 0 && !string.IsNullOrWhiteSpace(name))
                scheduleMap[code] = name;
        }
        _logger.LogInformation("PBS: fetched {Count} schedule(s)", schedules.Count);
        progress?.Report($"PBS: fetched {schedules.Count} schedule(s)");

        // Step 5: fetch organisations (for organisation_id -> name). Note: manufacturer_code (e.g.
        // "NE") is a separate short alpha code, not the numeric organisation_id — it can't be used
        // as a join key into /organisations, which is keyed by organisation_id.
        var orgCodes = items.Select(i => GetField(i, "organisation_id")).Where(c => c > 0).Distinct().ToHashSet();
        _logger.LogInformation("PBS: fetching {Count} organisation(s)...", orgCodes.Count);
        progress?.Report($"PBS: fetching {orgCodes.Count} organisation(s)...");
        var orgs = await FetchOrganisationsAsync(orgCodes, progress, cancellationToken);
        var orgMap = new Dictionary<int, string>();
        foreach (var o in orgs)
        {
            var id = GetField(o, "organisation_id");
            var name = GetString(o, "organisation_name");
            if (id > 0 && !string.IsNullOrWhiteSpace(name))
                orgMap[id] = name;
        }
        _logger.LogInformation("PBS: fetched {Count} organisation(s)", orgs.Count);
        progress?.Report($"PBS: fetched {orgs.Count} organisation(s)");

        // Step 6: assemble rows — one per item, stored verbatim.
        // AMT concept IDs are NOT fetched here (the amt-items endpoint has ~60k rows across 121
        // pages and the PBS API rate limit makes a full pull impractical). AMT codes can be
        // looked up on demand later via a separate per-item API call if needed.
        progress?.Report($"PBS: assembling {items.Count} row(s)...");
        var rows = new List<ParsedMedicineRow>();
        foreach (var item in items)
        {
            var brandName = GetString(item, "brand_name");
            if (string.IsNullOrWhiteSpace(brandName)) continue;

            var genericName = GetString(item, "li_drug_name");
            var liForm = GetString(item, "li_form");
            var pbsCode = GetString(item, "pbs_code");
            var programCode = GetString(item, "program_code");
            var isRpbs = string.Equals(programCode, "RP", StringComparison.OrdinalIgnoreCase);
            var organisationId = GetField(item, "organisation_id");
            var scheduleCode = GetField(item, "schedule_code");
            var benefitTypeCode = GetString(item, "benefit_type_code");
            var packSize = GetField(item, "pack_size");

            atcByPbsCode.TryGetValue(pbsCode ?? string.Empty, out var atcCode);
            var manufacturer = organisationId > 0 && orgMap.TryGetValue(organisationId, out var m) ? m : null;
            var schedule = MapSchedule(scheduleCode > 0 && scheduleMap.TryGetValue(scheduleCode, out var schedName) ? schedName : null);
            var (form, strength) = SplitFormAndStrength(liForm);

            var ingredients = !string.IsNullOrWhiteSpace(genericName) && genericName.Contains('/')
                ? genericName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : (!string.IsNullOrWhiteSpace(genericName) ? [genericName] : Array.Empty<string>());

            rows.Add(new ParsedMedicineRow(
                Name: brandName,
                GenericName: genericName,
                Strength: strength,
                Form: form,
                PackSize: packSize > 0 ? packSize.ToString() : null,
                Manufacturer: manufacturer,
                ArtgId: null,
                IsPbsListed: true,
                IsRpbsListed: isRpbs,
                RestrictionLevel: MapRestrictionLevel(benefitTypeCode),
                Schedule: schedule,
                ActiveIngredients: ingredients,
                PbsItemCode: pbsCode,
                AmtConceptId: null,
                AtcCode: atcCode));
        }

        _logger.LogInformation("PBS: assembled {Count} medicine row(s)", rows.Count);
        return rows;
    }

    private async Task<List<JsonElement>> FetchAllPagesAsync(string path, string label, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var results = new List<JsonElement>();
        var page = 1;
        while (true)
        {
            var url = $"{BaseUrl}{path}&limit={PageSize}&page={page}";
            var (data, totalPages) = await FetchPageWithRetry(url, progress, cancellationToken);
            results.AddRange(data);
            _logger.LogDebug("PBS: {Path} page {Page}/{TotalPages} — {Count} rows (total {Total})",
                path, page, totalPages, data.Count, results.Count);
            progress?.Report($"PBS: {label} — page {page}/{totalPages} ({results.Count} so far)");
            if (page >= totalPages || data.Count == 0) break;
            page++;
        }
        return results;
    }

    private async Task<List<JsonElement>> FetchAllForScheduleCodeAsync(string endpoint, HashSet<int> codes, string label, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var results = new List<JsonElement>();
        foreach (var code in codes)
        {
            var page = 1;
            while (true)
            {
                var url = $"{BaseUrl}{endpoint}?schedule_code={code}&limit={PageSize}&page={page}";
                var (data, totalPages) = await FetchPageWithRetry(url, progress, cancellationToken);
                results.AddRange(data);
                progress?.Report($"PBS: {label} — page {page}/{totalPages} ({results.Count} so far)");
                if (page >= totalPages || data.Count == 0) break;
                page++;
            }
        }
        return results;
    }

    // Live-verified 2026-08: the organisations endpoint hits 429 far more aggressively than
    // items/ATC/schedules (the API's rate limit is shared across all public-API users, not just
    // this process, so it's bursty and outside our control) — a 10,000+ item pull needing ~230
    // organisation-lookup requests at Chunk(10) lost most of its manufacturer coverage to retry
    // exhaustion (only 1,053 of 5,941 assembled rows got a name). Batching 50 IDs per filter query
    // cuts that to ~1/5th the request count for the same coverage.
    private const int OrganisationBatchSize = 50;

    private async Task<List<JsonElement>> FetchOrganisationsAsync(HashSet<int> orgIds, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (orgIds.Count == 0) return new List<JsonElement>();
        var results = new List<JsonElement>();
        var batches = orgIds.Chunk(OrganisationBatchSize).ToList();
        var batchNum = 0;
        foreach (var batch in batches)
        {
            batchNum++;
            var filter = string.Join(" or ", batch.Select(id => $"organisation_id eq {id}"));
            var url = $"{BaseUrl}organisations?filter={Uri.EscapeDataString(filter)}&limit={PageSize}";
            try
            {
                var (data, _) = await FetchPageWithRetry(url, progress, cancellationToken);
                results.AddRange(data);
                progress?.Report($"PBS: organisations — batch {batchNum}/{batches.Count} ({results.Count} so far)");
            }
            catch (OperationCanceledException)
            {
                // A user-requested cancel, not a batch failure — must propagate, unlike the soft-fail
                // catch below which is specifically for "this one enrichment lookup didn't work".
                throw;
            }
            catch (Exception ex)
            {
                // Organisation names are enrichment-only (they only fill in Manufacturer) — a failed
                // batch shouldn't abort an otherwise-successful ~15k-item pull, unlike items/ATC/
                // schedules below, which are core data and are allowed to propagate.
                _logger.LogWarning(ex, "PBS: organisation batch failed, affected items will have no manufacturer");
                progress?.Report($"PBS: organisations — batch {batchNum}/{batches.Count} FAILED (rate limit), continuing without it");
            }
        }
        return results;
    }

    private async Task<(List<JsonElement> Data, int TotalPages)> FetchPageWithRetry(string url, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attempt > 0)
            {
                // Exponential backoff: 10s, 20s, 40s, 80s
                var backoff = TimeSpan.FromSeconds(10 * Math.Pow(2, attempt - 1));
                _logger.LogWarning("PBS API retry {Attempt} for {Url} after backoff {Backoff}s",
                    attempt, url, backoff.TotalSeconds);
                progress?.Report($"PBS: rate limited — waiting {backoff.TotalSeconds:0}s before retry {attempt}/4...");
                await Task.Delay(backoff, cancellationToken);
            }

            await Task.Delay(RateLimitDelay, cancellationToken);

            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("PBS API returned 429 for {Url} (attempt {Attempt})", url, attempt + 1);
                progress?.Report("PBS: rate limited (429) — cooling down 15s...");
                // Extra cooldown after 429 — the API needs time to reset its rate-limit window
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                // A hard error (not a 429) — retrying the same request immediately won't help, and
                // silently returning empty here would look identical to "no more pages" to the
                // caller, truncating the pull without any signal. Throw instead so it surfaces as a
                // visible source failure.
                _logger.LogError("PBS API returned {StatusCode} for {Url}", (int)response.StatusCode, url);
                throw new HttpRequestException($"PBS API returned {(int)response.StatusCode} for {url}");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            var data = new List<JsonElement>();
            if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataEl.EnumerateArray())
                    data.Add(item.Clone());
            }

            var totalPages = 1;
            if (root.TryGetProperty("_meta", out var meta) &&
                meta.TryGetProperty("total_records", out var total) &&
                total.TryGetInt32(out var totalRecords))
            {
                totalPages = (int)Math.Ceiling((double)totalRecords / PageSize);
            }

            return (data, totalPages);
        }

        _logger.LogError("PBS API exhausted retries for {Url}", url);
        throw new HttpRequestException($"PBS API exhausted retries (repeated 429) for {url}");
    }

    private static string? GetString(JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }

    private static int GetField(JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val))
        {
            if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var n))
                return n;
            if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var s))
                return s;
        }
        return 0;
    }

    private static MedicineSchedule? MapSchedule(string? scheduleName)
    {
        if (string.IsNullOrWhiteSpace(scheduleName)) return null;
        var normalized = scheduleName.Replace("Schedule", "").Trim();
        return normalized switch
        {
            "2" => MedicineSchedule.S2,
            "3" => MedicineSchedule.S3,
            "4" => MedicineSchedule.S4,
            "8" => MedicineSchedule.S8,
            _ => null
        };
    }

    /// <summary>Maps benefit_type_code per the official PBS API Data Dictionary's "Four levels of
    /// restriction categorisation": U=Unrestricted, R=Restricted, A=Authority Required,
    /// S=Authority Required (Streamlined). S collapses into AuthorityRequired, matching
    /// PbsRestrictionLevel's own doc comment — this project already decided not to split Streamlined
    /// out as its own category.</summary>
    private static PbsRestrictionLevel? MapRestrictionLevel(string? benefitTypeCode) =>
        benefitTypeCode?.Trim().ToUpperInvariant() switch
        {
            "U" => PbsRestrictionLevel.Unrestricted,
            "R" => PbsRestrictionLevel.Restricted,
            "A" => PbsRestrictionLevel.AuthorityRequired,
            "S" => PbsRestrictionLevel.AuthorityRequired,
            _ => null
        };

    // Matches a leading form word/phrase followed by a dose expression, e.g. "Tablet 550 mg" or
    // "Capsule 75 mg (as mesilate)". Lazy on the form group so it stops at the first digit.
    private static readonly Regex FormStrengthPattern = new(@"^([A-Za-z][A-Za-z\-\s]*?)\s+(\d.*)$", RegexOptions.Compiled);

    /// <summary>Splits li_form into (Form, Strength) for the common single-active-ingredient case.
    /// Deliberately conservative: combination-product descriptions ("Tablet containing X 20 mg with Y
    /// 5 mg...") don't have one clean strength to extract, so those — and anything else the pattern
    /// doesn't confidently match — are left whole in Form with a null Strength, same as before this
    /// split existed. Splitting is a presentational parse of PBS's verbatim text, not a content
    /// change, so it's consistent with the "do not modify" licence condition.</summary>
    private static (string Form, string? Strength) SplitFormAndStrength(string? liForm)
    {
        if (string.IsNullOrWhiteSpace(liForm)) return (liForm ?? string.Empty, null);

        if (liForm.Contains("containing", StringComparison.OrdinalIgnoreCase) ||
            liForm.Contains(" with ", StringComparison.OrdinalIgnoreCase))
        {
            return (liForm, null);
        }

        var match = FormStrengthPattern.Match(liForm);
        return match.Success
            ? (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim())
            : (liForm, null);
    }
}

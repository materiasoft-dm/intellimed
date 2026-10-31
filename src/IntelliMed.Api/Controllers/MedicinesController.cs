using IntelliMed.Api.Authorization;
using System.Security.Claims;
using System.Text;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Medicine reference-data catalog: search/typeahead, manual CSV import (for hand-edited or
/// externally-obtained files), and CRUD for Manual entries. "Sync" fetches from every registered
/// IMedicineSourceProvider and imports the result directly (assemble -> import -> Wikidata ingredient
/// enrichment, all in one background job) — there is no separate "download a CSV to review, then
/// re-upload it" step; MedicineImportService's upsert-by-key logic and the "no longer PBS-listed"
/// delist check are the safety net, proven idempotent by repeated live testing rather than a manual
/// review gate. "Export" is unrelated — it reads the current database, not external sources.
/// </summary>
[ApiController]
[Route("api/medicines")]
public class MedicinesController : ControllerBase
{
    private const string ImportPageKey = "admin/medicines";

    private readonly IMedicineRepository _repository;
    private readonly IMedicineImportService _importService;
    private readonly IIngredientEnrichmentService _enrichmentService;
    private readonly IArtgEnrichmentService _artgEnrichmentService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppDbContext _context;

    public MedicinesController(
        IMedicineRepository repository,
        IMedicineImportService importService,
        IIngredientEnrichmentService enrichmentService,
        IArtgEnrichmentService artgEnrichmentService,
        IServiceScopeFactory scopeFactory,
        AppDbContext context)
    {
        _artgEnrichmentService = artgEnrichmentService;
        _repository = repository;
        _importService = importService;
        _enrichmentService = enrichmentService;
        _scopeFactory = scopeFactory;
        _context = context;
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<MedicineDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? query, [FromQuery] MedicineSource? source,
        [FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        // skip/take page the admin catalogue list; the prescribing search keeps the default first 20.
        var items = await _repository.SearchAsync(query ?? string.Empty, source, Math.Clamp(take, 1, 200), Math.Max(0, skip));
        return Ok(items);
    }

    [HttpGet("export")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportCsv()
    {
        if (!await HasAccessAsync()) return Forbid();

        var csv = await _repository.ExportCsvAsync();
        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"medicine-catalogue-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    [HttpPost("import")]
    [Authorize]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(MedicineImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (!await HasAccessAsync()) return Forbid();
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

        await using var stream = file.OpenReadStream();
        var result = await _importService.ImportAsync(stream);
        return Ok(result);
    }

    /// <summary>
    /// Backfills poisons schedule, ARTG ID, route and dosage form from a TGA ARTG "Medicines" export.
    /// PBS publishes no scheduling, so without this every synced medicine has a null Schedule and the
    /// prescribing wizard's Schedule 8 gate never fires — see MEDICINE_DATA_ENRICHMENT.md §1.
    ///
    /// The size limit is higher than the CSV import's because the full ARTG Medicines export runs to
    /// roughly 95 MB — most of it Indications/Warnings free text the parser streams past without
    /// retaining.
    /// </summary>
    [HttpPost("enrich-artg")]
    [Authorize]
    [RequestSizeLimit(200_000_000)]
    [ProducesResponseType(typeof(ArtgEnrichmentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EnrichFromArtg(IFormFile file, CancellationToken cancellationToken)
    {
        if (!await HasAccessAsync()) return Forbid();
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var result = await _artgEnrichmentService.EnrichAsync(reader, cancellationToken);

        if (result.ArtgRows == 0)
        {
            return BadRequest(
                "No ARTG rows found. Export from the ARTG Search Visualisation Tool with the " +
                "'Medicines' filter applied and save as CSV — the unfiltered export has only 8 " +
                "columns and carries no scheduling.");
        }

        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MedicineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateMedicineDto dto)
    {
        if (!await HasAccessAsync()) return Forbid();

        var id = await _repository.CreateAsync(dto);
        var created = await _repository.GetByIdAsync(id);
        return Ok(created);
    }

    [HttpPut("{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMedicineDto dto)
    {
        if (!await HasAccessAsync()) return Forbid();

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();
        if (existing.Source != MedicineSource.Manual)
            return BadRequest("Synced medicines can't be edited manually — only manually-created entries can.");

        await _repository.UpdateAsync(id, dto);
        return Ok();
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id)
    {
        if (!await HasAccessAsync()) return Forbid();

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();
        if (existing.Source != MedicineSource.Manual)
            return BadRequest("Synced medicines can't be deactivated manually — only manually-created entries can.");

        await _repository.DeactivateAsync(id);
        return Ok();
    }

    /// <summary>Kicks off a background job that assembles from every registered source, imports the
    /// result directly, then runs Wikidata ingredient enrichment — all in one run, polled via
    /// GetSyncProgress and stoppable via CancelSync. Takes ~10-15 minutes, almost entirely PBS API
    /// rate-limit waiting, not local work.</summary>
    [HttpPost("sync")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SyncFromSources()
    {
        if (!await HasAccessAsync()) return Forbid();

        var jobId = Guid.NewGuid().ToString("N");
        var state = new SyncJobState();
        lock (SyncProgressStore.Jobs)
        {
            SyncProgressStore.Jobs[jobId] = state;
        }

        IProgress<string> progress = new Progress<string>(msg =>
        {
            lock (SyncProgressStore.Jobs)
            {
                state.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] {msg}");
                state.Status = msg;
            }
        });

        var cancellationToken = state.Cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                // A fresh DI scope for the whole job, resolved fresh from the scope rather than
                // using this controller's own constructor-injected services: those are tied to the
                // HTTP request's scope, which is disposed (along with its AppDbContext) as soon as
                // this action returns — long before this job, still running for ~15 more minutes,
                // gets to the AppDbContext-backed import/enrichment steps. Mirrors
                // FeeScheduleAutoImportBackgroundService's established pattern for this exact problem.
                using var scope = _scopeFactory.CreateScope();
                var csvAssemblyService = scope.ServiceProvider.GetRequiredService<IMedicineCsvAssemblyService>();
                var importService = scope.ServiceProvider.GetRequiredService<IMedicineImportService>();
                var enrichmentService = scope.ServiceProvider.GetRequiredService<IIngredientEnrichmentService>();

                var csv = await csvAssemblyService.AssembleCsvAsync(progress, cancellationToken);

                progress.Report("Importing into catalogue...");
                using var csvStream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
                var importResult = await importService.ImportAsync(csvStream);
                progress.Report(importResult.Message ?? "Import complete.");
                // Recorded immediately, not batched with the enrichment result at the end of the try
                // block — a cancel landing right after this (ImportAsync itself isn't
                // cancellation-aware, so the only checkpoint is the ThrowIfCancellationRequested()
                // below) would otherwise report IsCancelled with the import silently dropped from the
                // job state, even though it already committed to the database.
                lock (SyncProgressStore.Jobs) { state.ImportResult = importResult; }

                cancellationToken.ThrowIfCancellationRequested();

                progress.Report("Enriching ingredients from Wikidata...");
                var enrichmentResult = await enrichmentService.EnrichFromWikidataAsync();
                progress.Report(
                    $"Enriched {enrichmentResult.Enriched} of {enrichmentResult.TotalProcessed} ingredient(s), " +
                    $"{enrichmentResult.NotFound} not found on Wikidata.");
                lock (SyncProgressStore.Jobs) { state.EnrichmentResult = enrichmentResult; }

                lock (SyncProgressStore.Jobs)
                {
                    state.IsComplete = true;
                    state.Status = "Sync complete.";
                }
            }
            catch (OperationCanceledException)
            {
                // Leaves whatever ImportResult/EnrichmentResult were already recorded above intact —
                // a cancel that lands between phases should still show what actually happened, not
                // hide it behind IsCancelled.
                lock (SyncProgressStore.Jobs)
                {
                    state.IsCancelled = true;
                    state.IsComplete = true;
                    state.Status = "Cancelled.";
                }
            }
            catch (Exception ex)
            {
                lock (SyncProgressStore.Jobs)
                {
                    state.Error = ex.Message;
                    state.IsComplete = true;
                    state.Status = "Failed.";
                }
            }
        }, CancellationToken.None); // the outer Task.Run itself must not be cancelled by the request's own lifetime

        return Ok(new { jobId });
    }

    [HttpGet("sync/{jobId}/progress")]
    [Authorize]
    [ProducesResponseType(typeof(SyncProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSyncProgress(string jobId)
    {
        lock (SyncProgressStore.Jobs)
        {
            if (!SyncProgressStore.Jobs.TryGetValue(jobId, out var state))
                return NotFound();

            return Ok(new SyncProgressDto
            {
                IsComplete = state.IsComplete,
                IsCancelled = state.IsCancelled,
                Status = state.Status,
                Log = state.Log.ToList(),
                Error = state.Error,
                ImportResult = state.ImportResult,
                EnrichmentResult = state.EnrichmentResult
            });
        }
    }

    [HttpPost("sync/{jobId}/cancel")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelSync(string jobId)
    {
        if (!await HasAccessAsync()) return Forbid();

        lock (SyncProgressStore.Jobs)
        {
            if (!SyncProgressStore.Jobs.TryGetValue(jobId, out var state))
                return NotFound();
            if (!state.IsComplete)
                state.Cts.Cancel();
        }
        return Ok();
    }

    /// <summary>Still directly callable (e.g. for scripting) even though the admin page no longer
    /// shows a standalone button for it — Sync now runs this automatically as its last step.</summary>
    [HttpPost("ingredients/enrich")]
    [Authorize]
    [ProducesResponseType(typeof(IngredientEnrichmentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EnrichIngredients()
    {
        if (!await HasAccessAsync()) return Forbid();

        var result = await _enrichmentService.EnrichFromWikidataAsync();
        return Ok(result);
    }

    private Task<bool> HasAccessAsync() =>
        RolePermissions.HasAccessAsync(_context, User, ImportPageKey);
}

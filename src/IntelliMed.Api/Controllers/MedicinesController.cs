using System.Security.Claims;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Medicine reference-data catalog: search/typeahead, plus an admin-triggered CSV import. See
/// MedicineImportService for why this is CSV-based rather than a live government feed like
/// BillingItemsController's MBS sync.
/// </summary>
[ApiController]
[Route("api/medicines")]
public class MedicinesController : ControllerBase
{
    private const string ImportPageKey = "admin/medicines";

    private readonly IMedicineRepository _repository;
    private readonly IMedicineImportService _importService;
    private readonly IIngredientEnrichmentService _enrichmentService;
    private readonly AppDbContext _context;

    public MedicinesController(
        IMedicineRepository repository,
        IMedicineImportService importService,
        IIngredientEnrichmentService enrichmentService,
        AppDbContext context)
    {
        _repository = repository;
        _importService = importService;
        _enrichmentService = enrichmentService;
        _context = context;
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<MedicineDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? query, [FromQuery] MedicineSource? source)
    {
        var items = await _repository.SearchAsync(query ?? string.Empty, source);
        return Ok(items);
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

    private async Task<bool> HasAccessAsync()
    {
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        if (roles.Contains("SuperAdmin")) return true;

        return await _context.RolePermissions
            .AnyAsync(p => roles.Contains(p.RoleName) && p.PageKey == ImportPageKey);
    }
}

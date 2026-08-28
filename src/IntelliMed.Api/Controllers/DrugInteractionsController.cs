using IntelliMed.Api.Authorization;
using System.Security.Claims;
using System.Text;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Admin management of the drug-interaction reference table — list/create/update plus CSV import/
/// export. Ships empty (see DrugInteractionImportService); every action here is an admin concern, not
/// something end-user UI browses directly (MedicationInteractionChecker is the only other consumer,
/// and it reads via IDrugInteractionRepository directly, not this controller), so unlike
/// MedicinesController's public Search, everything here is gated.
/// </summary>
[ApiController]
[Route("api/drug-interactions")]
public class DrugInteractionsController : ControllerBase
{
    private const string PageKey = "admin/drug-interactions";

    private readonly IDrugInteractionRepository _repository;
    private readonly IDrugInteractionImportService _importService;
    private readonly AppDbContext _context;

    public DrugInteractionsController(
        IDrugInteractionRepository repository,
        IDrugInteractionImportService importService,
        AppDbContext context)
    {
        _repository = repository;
        _importService = importService;
        _context = context;
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<DrugInteractionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        if (!await HasAccessAsync()) return Forbid();

        var items = await _repository.GetAllAsync();
        return Ok(items);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateDrugInteractionDto dto)
    {
        if (!await HasAccessAsync()) return Forbid();

        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAll), new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDrugInteractionDto dto)
    {
        if (!await HasAccessAsync()) return Forbid();

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }

    [HttpPost("import")]
    [Authorize]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType(typeof(DrugInteractionImportResultDto), StatusCodes.Status200OK)]
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

    [HttpGet("export")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportCsv()
    {
        if (!await HasAccessAsync()) return Forbid();

        var items = await _repository.GetAllAsync(take: int.MaxValue);
        var sb = new StringBuilder();
        sb.AppendLine("IngredientA,IngredientB,Severity,Description,SourceDataset");
        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                CsvEscape(item.IngredientAName),
                CsvEscape(item.IngredientBName),
                item.Severity.ToString(),
                CsvEscape(item.Description ?? string.Empty),
                CsvEscape(item.SourceDataset ?? string.Empty)
            }));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"drug-interactions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private static string CsvEscape(string field) =>
        field.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;

    private Task<bool> HasAccessAsync() =>
        RolePermissions.HasAccessAsync(_context, User, PageKey);
}

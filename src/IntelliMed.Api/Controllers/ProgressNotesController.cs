using IntelliMed.Api.Authorization;
using System.Security.Claims;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Client-scoped progress notes: list/create/update-while-draft/finalize/delete-while-draft/add-addendum.
/// No delete once Finalized — see ProgressNote.cs. Mutating actions are gated by a real HasAccessAsync()
/// check (not just RolePermissions-as-nav-filter), same deliberate deviation as PrescriptionsController,
/// given this produces a legal clinical record.
/// </summary>
[ApiController]
[Route("api/clients/{clientId:int}/progress-notes")]
public class ProgressNotesController : ControllerBase
{
    private readonly IProgressNoteRepository _progressNoteRepository;
    private readonly AppDbContext _context;

    public ProgressNotesController(IProgressNoteRepository progressNoteRepository, AppDbContext context)
    {
        _progressNoteRepository = progressNoteRepository;
        _context = context;
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ProgressNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByClient(int clientId)
    {
        if (!await HasAccessAsync("progress-notes")) return Forbid();

        var notes = await _progressNoteRepository.GetByClientIdAsync(clientId);
        return Ok(notes);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(ProgressNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int clientId, int id)
    {
        if (!await HasAccessAsync("progress-notes")) return Forbid();

        var note = await _progressNoteRepository.GetByIdAsync(id);
        if (note == null || note.ClientId != clientId) return NotFound();

        return Ok(note);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(int clientId, [FromBody] CreateProgressNoteDto dto)
    {
        if (!await HasAccessAsync("progress-notes/create")) return Forbid();

        dto.ClientId = clientId;
        var id = await _progressNoteRepository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { clientId, id }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int clientId, int id, [FromBody] UpdateProgressNoteDto dto)
    {
        if (!await HasAccessAsync("progress-notes/edit")) return Forbid();

        var existing = await _progressNoteRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _progressNoteRepository.UpdateAsync(id, dto);
        return NoContent();
    }

    [HttpPost("{id:int}/finalize")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Finalize(int clientId, int id, [FromBody] FinalizeProgressNoteDto dto)
    {
        if (!await HasAccessAsync("progress-notes/edit")) return Forbid();

        var existing = await _progressNoteRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _progressNoteRepository.FinalizeAsync(id, dto);
        return NoContent();
    }

    [HttpPost("{id:int}/addenda")]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAddendum(int clientId, int id, [FromBody] AddProgressNoteAddendumDto dto)
    {
        if (!await HasAccessAsync("progress-notes/edit")) return Forbid();

        var existing = await _progressNoteRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        var addendumId = await _progressNoteRepository.AddAddendumAsync(id, dto);
        return CreatedAtAction(nameof(GetById), new { clientId, id }, new { addendumId });
    }

    /// <summary>Only reachable while Status == Draft — see ProgressNoteRepository.DeleteAsync.</summary>
    [HttpDelete("{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int clientId, int id)
    {
        if (!await HasAccessAsync("progress-notes/edit")) return Forbid();

        var existing = await _progressNoteRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _progressNoteRepository.DeleteAsync(id);
        return NoContent();
    }

    private Task<bool> HasAccessAsync(string pageKey) =>
        RolePermissions.HasAccessAsync(_context, User, pageKey);
}

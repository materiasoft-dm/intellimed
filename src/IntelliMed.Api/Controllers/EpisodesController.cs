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
/// Client-scoped episodes — the grouping of ProgressNotes around one ongoing clinical issue (see
/// Episode.cs). No dedicated PageKey; rides under the same progress-notes/* gates as
/// ProgressNotesController, tab-only like ClientReferral/ClientAllergy.
/// </summary>
[ApiController]
[Route("api/clients/{clientId:int}/episodes")]
public class EpisodesController : ControllerBase
{
    private readonly IEpisodeRepository _episodeRepository;
    private readonly AppDbContext _context;

    public EpisodesController(IEpisodeRepository episodeRepository, AppDbContext context)
    {
        _episodeRepository = episodeRepository;
        _context = context;
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<EpisodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByClient(int clientId, [FromQuery] bool includeArchived = false)
    {
        if (!await HasAccessAsync("progress-notes")) return Forbid();

        var episodes = await _episodeRepository.GetByClientIdAsync(clientId, includeArchived);
        return Ok(episodes);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(int clientId, [FromBody] CreateEpisodeDto dto)
    {
        if (!await HasAccessAsync("progress-notes/create")) return Forbid();

        dto.ClientId = clientId;
        var id = await _episodeRepository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetByClient), new { clientId }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int clientId, int id, [FromBody] UpdateEpisodeDto dto)
    {
        if (!await HasAccessAsync("progress-notes/edit")) return Forbid();

        var existing = await _episodeRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _episodeRepository.UpdateAsync(id, dto);
        return NoContent();
    }

    [HttpPost("{id:int}/resolve")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(int clientId, int id)
    {
        if (!await HasAccessAsync("progress-notes/edit")) return Forbid();

        var existing = await _episodeRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _episodeRepository.ResolveAsync(id);
        return NoContent();
    }

    [HttpPost("{id:int}/reopen")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reopen(int clientId, int id)
    {
        if (!await HasAccessAsync("progress-notes/edit")) return Forbid();

        var existing = await _episodeRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _episodeRepository.ReopenAsync(id);
        return NoContent();
    }

    [HttpPost("{id:int}/archive")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int clientId, int id)
    {
        if (!await HasAccessAsync("progress-notes/edit")) return Forbid();

        var existing = await _episodeRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _episodeRepository.ArchiveAsync(id);
        return NoContent();
    }

    private Task<bool> HasAccessAsync(string pageKey) =>
        RolePermissions.HasAccessAsync(_context, User, pageKey);
}

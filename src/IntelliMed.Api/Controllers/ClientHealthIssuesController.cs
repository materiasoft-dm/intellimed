using IntelliMed.Api.Authorization;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// The client's problem list — see ClientHealthIssue.cs. Mutating actions are gated by a real
/// RolePermissions check rather than relying on the nav filter alone, matching the discipline
/// PrescriptionsController/ProgressNotesController use for clinical records.
///
/// No Delete route: an issue that turns out to be wrong is archived, not erased, so the clinical
/// record stays auditable.
/// </summary>
[ApiController]
[Route("api/clients/{clientId:int}/health-issues")]
public class ClientHealthIssuesController : ControllerBase
{
    private readonly IClientHealthIssueRepository _repository;
    private readonly IProgressNoteRepository _progressNoteRepository;
    private readonly AppDbContext _context;

    public ClientHealthIssuesController(
        IClientHealthIssueRepository repository,
        IProgressNoteRepository progressNoteRepository,
        AppDbContext context)
    {
        _repository = repository;
        _progressNoteRepository = progressNoteRepository;
        _context = context;
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ClientHealthIssueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByClient(int clientId, [FromQuery] bool includeArchived = false)
    {
        if (!await HasAccessAsync("health-issues")) return Forbid();

        return Ok(await _repository.GetByClientIdAsync(clientId, includeArchived));
    }

    [HttpGet("summary")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ClientHealthIssueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary(int clientId)
    {
        if (!await HasAccessAsync("health-issues")) return Forbid();

        return Ok(await _repository.GetSummaryForClientAsync(clientId));
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(int clientId, [FromBody] CreateClientHealthIssueDto dto)
    {
        if (!await HasAccessAsync("health-issues/create")) return Forbid();

        dto.ClientId = clientId;
        var id = await _repository.CreateAsync(dto);

        // Mirrors Medinet's "Add to reason" button, using the same mechanism PrescriptionsController
        // .Issue uses to log "Prescribed: …" — no-ops when there's no linked appointment or no note
        // open on it.
        if (dto.AppointmentId.HasValue)
        {
            var practitionerId = await ResolvePractitionerIdAsync(dto.AppointmentId.Value);
            if (practitionerId.HasValue)
            {
                await _progressNoteRepository.AppendSystemEntryAsync(
                    dto.AppointmentId.Value,
                    $"Health issue recorded: {dto.Condition}",
                    "HealthIssue",
                    id,
                    practitionerId.Value);
            }
        }

        return CreatedAtAction(nameof(GetByClient), new { clientId }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int clientId, int id, [FromBody] UpdateClientHealthIssueDto dto)
    {
        if (!await HasAccessAsync("health-issues/edit")) return Forbid();

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }

    [HttpPost("{id:int}/archive")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int clientId, int id)
    {
        if (!await HasAccessAsync("health-issues/edit")) return Forbid();

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _repository.ArchiveAsync(id);
        return NoContent();
    }

    /// <summary>The appointment's practitioner is the attribution used for the auto-logged note
    /// entry — the issue itself carries no prescriber-equivalent field.</summary>
    private async Task<int?> ResolvePractitionerIdAsync(int appointmentId)
    {
        var appointment = await _context.Appointments.FindAsync(appointmentId);
        return appointment?.PractitionerId;
    }

    private Task<bool> HasAccessAsync(string pageKey) =>
        RolePermissions.HasAccessAsync(_context, User, pageKey);
}

using IntelliMed.Api.Authorization;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Application-wide settings — password/lockout policy, session length, new-user defaults and
/// backup retention. Distinct from ClinicSettingsController, which covers per-practice details.
/// Gated on the "admin/settings" page key.
/// </summary>
[ApiController]
[Route("api/system-settings")]
[Authorize]
public class SystemSettingsController : ControllerBase
{
    private const string PageKey = "admin/settings";

    private readonly ISystemSettingsRepository _repository;
    private readonly AppDbContext _context;
    private readonly ILogger<SystemSettingsController> _logger;

    public SystemSettingsController(
        ISystemSettingsRepository repository,
        AppDbContext context,
        ILogger<SystemSettingsController> logger)
    {
        _repository = repository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(SystemSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get()
    {
        if (!await HasAccessAsync()) return Forbid();

        return Ok(await _repository.GetAsync());
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update([FromBody] UpdateSystemSettingsDto dto)
    {
        if (!await HasAccessAsync()) return Forbid();

        try
        {
            await _repository.UpdateAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            // Range validation from the repository — surfaced to the admin verbatim so they can see
            // which value was out of bounds.
            return BadRequest(new { message = ex.Message });
        }

        _logger.LogInformation("System settings updated by {User}", User.Identity?.Name);
        return NoContent();
    }

    private Task<bool> HasAccessAsync() => RolePermissions.HasAccessAsync(_context, User, PageKey);
}

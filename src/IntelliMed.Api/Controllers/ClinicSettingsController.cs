using System.Security.Claims;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Practice-wide clinic settings (single row). Access isn't restricted to a fixed role list —
/// any role that's been granted the "clinic-settings" page via Role Configuration can use it,
/// same dynamic check GetMyAccessiblePages uses for nav visibility.
/// </summary>
[ApiController]
[Route("api/clinic-settings")]
[Authorize]
public class ClinicSettingsController : ControllerBase
{
    private const string PageKey = "clinic-settings";

    private readonly IClinicSettingsRepository _repository;
    private readonly AppDbContext _context;

    public ClinicSettingsController(IClinicSettingsRepository repository, AppDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ClinicSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClinicSettingsDto>> Get()
    {
        if (!await HasAccessAsync()) return Forbid();
        return Ok(await _repository.GetSingletonAsync());
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update([FromBody] UpdateClinicSettingsRequest request)
    {
        if (!await HasAccessAsync()) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await _repository.UpdateSingletonAsync(request);
        return NoContent();
    }

    private async Task<bool> HasAccessAsync()
    {
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        if (roles.Contains("SuperAdmin")) return true;

        return await _context.RolePermissions
            .AnyAsync(p => roles.Contains(p.RoleName) && p.PageKey == PageKey);
    }
}

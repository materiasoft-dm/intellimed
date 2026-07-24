using System.Security.Claims;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Manages practice locations (clinics) and which users belong to each.
/// Full management is gated dynamically by the "clinic-manager" page permission (not a fixed role list) —
/// same mechanism ClinicSettingsController uses. "my-clinics" is available to any authenticated user,
/// since it only powers the clinic switcher for whichever clinics they've been assigned to.
/// </summary>
[ApiController]
[Route("api/clinics")]
[Authorize]
public class ClinicsController : ControllerBase
{
    private const string PageKey = "clinic-manager";

    private readonly IClinicRepository _repository;
    private readonly AppDbContext _context;

    public ClinicsController(IClinicRepository repository, AppDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    [HttpGet("my-clinics")]
    [ProducesResponseType(typeof(IEnumerable<MyClinicDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MyClinicDto>>> GetMyClinics()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        return Ok(await _repository.GetMyClinicsAsync(userId));
    }

    /// <summary>
    /// Lightweight lookup of all active clinics (id + name only), for populating pickers like
    /// the clinic-assignment checklist on the create/edit user dialog. Any authenticated user can
    /// read this — it's not the full management surface, so it isn't gated by "clinic-manager".
    /// </summary>
    [HttpGet("lookup")]
    [ProducesResponseType(typeof(IEnumerable<MyClinicDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MyClinicDto>>> GetLookup()
    {
        return Ok(await _repository.GetLookupAsync());
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClinicDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ClinicDto>>> GetAll()
    {
        if (!await HasAccessAsync()) return Forbid();
        return Ok(await _repository.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClinicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicDto>> GetById(int id)
    {
        if (!await HasAccessAsync()) return Forbid();
        var clinic = await _repository.GetDtoByIdAsync(id);
        if (clinic == null) return NotFound();
        return Ok(clinic);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateClinicDto dto)
    {
        if (!await HasAccessAsync()) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateClinicDto dto)
    {
        if (!await HasAccessAsync()) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existing = await _repository.GetDtoByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }

    [HttpGet("{id:int}/users")]
    [ProducesResponseType(typeof(IEnumerable<ClinicUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ClinicUserDto>>> GetClinicUsers(int id)
    {
        if (!await HasAccessAsync()) return Forbid();
        var existing = await _repository.GetDtoByIdAsync(id);
        if (existing == null) return NotFound();

        return Ok(await _repository.GetClinicUsersAsync(id));
    }

    [HttpPut("{id:int}/users")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetClinicUsers(int id, [FromBody] SetClinicUsersRequest request)
    {
        if (!await HasAccessAsync()) return Forbid();
        var existing = await _repository.GetDtoByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.SetClinicUsersAsync(id, request.UserIds);
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

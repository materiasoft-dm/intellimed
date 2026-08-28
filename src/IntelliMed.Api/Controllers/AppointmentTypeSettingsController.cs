using IntelliMed.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Admin CRUD for the clinic-configurable appointment-type/duration-preset catalogue.
///
/// The mutating actions are gated on the "admin/appointment-types" page key so the toggle at
/// /admin/roles actually controls them — they previously carried [Authorize(Roles = "SuperAdmin,
/// Admin")], which ignored the catalogue entirely. The GETs stay open to any authenticated user
/// because the booking form reads them to populate its type dropdown.
/// </summary>
[ApiController]
[Route("api/appointment-type-settings")]
public class AppointmentTypeSettingsController : ControllerBase
{
    private const string PageKey = "admin/appointment-types";

    private readonly IAppointmentTypeSettingRepository _repository;
    private readonly AppDbContext _context;

    public AppointmentTypeSettingsController(IAppointmentTypeSettingRepository repository, AppDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    private Task<bool> HasAccessAsync() => RolePermissions.HasAccessAsync(_context, User, PageKey);

    /// <summary>List active appointment types for the current clinic (also used to populate the booking form's type dropdown — any logged-in user, not just admins).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppointmentTypeSettingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActive()
    {
        var settings = await _repository.GetAllActiveAsync(GetCurrentClinicId());
        return Ok(settings);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AppointmentTypeSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var setting = await _repository.GetByIdAsync(id);
        if (setting == null) return NotFound();
        return Ok(setting);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentTypeSettingDto dto)
    {
        if (!await HasAccessAsync()) return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        dto.ClinicId = GetCurrentClinicId() ?? 1;
        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentTypeSettingDto dto)
    {
        if (!await HasAccessAsync()) return Forbid();

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }

    [HttpPost("{id:int}/archive")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id)
    {
        if (!await HasAccessAsync()) return Forbid();

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.ArchiveAsync(id);
        return NoContent();
    }

    private int? GetCurrentClinicId()
    {
        if (Request.Headers.TryGetValue("X-Clinic-Id", out var value) &&
            int.TryParse(value.ToString(), out var clinicId))
        {
            return clinicId;
        }
        return null;
    }
}

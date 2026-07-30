using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Admin CRUD for the clinic-configurable appointment-type/duration-preset catalogue.
/// </summary>
[ApiController]
[Route("api/appointment-type-settings")]
public class AppointmentTypeSettingsController : ControllerBase
{
    private readonly IAppointmentTypeSettingRepository _repository;

    public AppointmentTypeSettingsController(IAppointmentTypeSettingRepository repository)
    {
        _repository = repository;
    }

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
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentTypeSettingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        dto.ClinicId = GetCurrentClinicId() ?? 1;
        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentTypeSettingDto dto)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }

    [HttpPost("{id:int}/archive")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id)
    {
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

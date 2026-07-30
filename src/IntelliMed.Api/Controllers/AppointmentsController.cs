using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IAppointmentReminderService _reminderService;
    private readonly IPractitionerRepository _practitionerRepository;
    private readonly IProviderScheduleRepository _providerScheduleRepository;

    public AppointmentsController(
        IAppointmentRepository appointmentRepository,
        IAppointmentReminderService reminderService,
        IPractitionerRepository practitionerRepository,
        IProviderScheduleRepository providerScheduleRepository)
    {
        _appointmentRepository = appointmentRepository;
        _reminderService = reminderService;
        _practitionerRepository = practitionerRepository;
        _providerScheduleRepository = providerScheduleRepository;
    }

    /// <summary>
    /// Search appointments with optional client/practitioner/date/status filters, returning paged results.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<AppointmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] AppointmentSearchDto search)
    {
        search.ClinicId = GetCurrentClinicId();
        var (items, totalCount) = await _appointmentRepository.GetPagedAsync(search);
        return Ok(new PagedResult<AppointmentDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = search.Page,
            PageSize = search.PageSize
        });
    }

    /// <summary>
    /// Today's (or a given date's) Arrived/InProgress appointments across practitioners for the current clinic, sorted by arrival time.
    /// </summary>
    [HttpGet("waiting-room")]
    [ProducesResponseType(typeof(IEnumerable<AppointmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWaitingRoom([FromQuery] DateTime? date)
    {
        var appointments = await _appointmentRepository.GetWaitingRoomAsync(GetCurrentClinicId(), date ?? DateTime.Today);
        return Ok(appointments);
    }

    /// <summary>
    /// Get a single appointment with its client and practitioner.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null) return NotFound();
        return Ok(appointment);
    }

    /// <summary>
    /// All occurrences that share a recurring series id.
    /// </summary>
    [HttpGet("series/{seriesId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppointmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSeries(Guid seriesId)
    {
        var appointments = await _appointmentRepository.GetBySeriesIdAsync(seriesId);
        return Ok(appointments);
    }

    /// <summary>
    /// Whether a practitioner is free for the given date/time range (used for the booking form's double-booking warning).
    /// </summary>
    [HttpGet("availability")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckAvailability([FromQuery] int practitionerId, [FromQuery] DateTime date, [FromQuery] TimeSpan startTime, [FromQuery] TimeSpan endTime, [FromQuery] int? excludeAppointmentId)
    {
        var available = await _appointmentRepository.IsTimeSlotAvailableAsync(practitionerId, date, startTime, endTime, excludeAppointmentId);
        return Ok(available);
    }

    /// <summary>
    /// Advisory (non-blocking) check: does the practitioner have a configured working-hours schedule that
    /// covers this date? Soft-matches the practitioner to a logged-in user by email — returns null
    /// (no opinion) if the practitioner has no linked user account or no schedule configured.
    /// </summary>
    [HttpGet("practitioner-schedule")]
    [ProducesResponseType(typeof(ProviderScheduleDayDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPractitionerSchedule([FromQuery] int practitionerId, [FromQuery] DateTime date)
    {
        var practitioner = await _practitionerRepository.GetByIdAsync(practitionerId);
        if (practitioner == null || string.IsNullOrWhiteSpace(practitioner.Email)) return Ok(null);

        var schedule = await _providerScheduleRepository.GetByPractitionerEmailAsync(practitioner.Email, date.DayOfWeek);
        return Ok(schedule);
    }

    /// <summary>
    /// Book a single appointment.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        dto.ClinicId = GetCurrentClinicId() ?? 1;
        var id = await _appointmentRepository.CreateAsync(dto);

        var created = await _appointmentRepository.GetByIdAsync(id);
        if (created != null) await _reminderService.NotifyUpcomingAsync(created);

        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>
    /// Book a recurring series — materializes one appointment row per occurrence, all sharing a new series id.
    /// </summary>
    [HttpPost("series")]
    [ProducesResponseType(typeof(IEnumerable<int>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSeries([FromBody] CreateAppointmentSeriesDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.Occurrences == null && dto.UntilDate == null)
            return BadRequest("Either Occurrences or UntilDate must be set.");

        dto.ClinicId = GetCurrentClinicId() ?? 1;
        var ids = await _appointmentRepository.CreateSeriesAsync(dto);
        return StatusCode(StatusCodes.Status201Created, ids);
    }

    /// <summary>
    /// Update an appointment's booking details.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _appointmentRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _appointmentRepository.UpdateAsync(id, dto);
        return NoContent();
    }

    /// <summary>
    /// Transition an appointment's status (Confirm, Arrived, Start, Complete, Cancel, No-show) — sets the matching visit-lifecycle timestamp server-side.
    /// </summary>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateAppointmentStatusRequest request)
    {
        var existing = await _appointmentRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _appointmentRepository.UpdateStatusAsync(id, request.Status);
        return NoContent();
    }

    /// <summary>
    /// Move an appointment to a new date/time (drag-to-reschedule on the calendar), re-validating the slot is free.
    /// </summary>
    [HttpPut("{id:int}/reschedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reschedule(int id, [FromBody] RescheduleAppointmentDto dto)
    {
        var existing = await _appointmentRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        try
        {
            await _appointmentRepository.RescheduleAsync(id, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        var updated = await _appointmentRepository.GetByIdAsync(id);
        if (updated != null) await _reminderService.NotifyUpcomingAsync(updated);

        return NoContent();
    }

    /// <summary>
    /// Cancel/delete a single appointment.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _appointmentRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _appointmentRepository.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Cancel a whole recurring series, or just this-and-following occurrences when fromDate is given.
    /// </summary>
    [HttpDelete("series/{seriesId:guid}")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteSeries(Guid seriesId, [FromQuery] DateTime? fromDate)
    {
        var count = await _appointmentRepository.DeleteSeriesAsync(seriesId, fromDate);
        return Ok(count);
    }

    /// <summary>
    /// Reads the caller's currently selected clinic from the X-Clinic-Id header set by the Web client.
    /// </summary>
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

public class UpdateAppointmentStatusRequest
{
    public AppointmentStatus Status { get; set; }
}

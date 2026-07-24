using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/fee-schedules")]
public class FeeSchedulesController : ControllerBase
{
    private readonly IFeeScheduleRepository _repository;

    public FeeSchedulesController(IFeeScheduleRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Search fee schedules with optional code/description filters and archived toggle, returning paged results.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<FeeScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] FeeScheduleSearchDto search)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(search);
        return Ok(new PagedResult<FeeScheduleDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = search.Page,
            PageSize = search.PageSize
        });
    }

    /// <summary>
    /// Lightweight list of active fee schedules, used to populate the Health Fund/Fee Table dropdowns elsewhere.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<FeeScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActive()
    {
        var schedules = await _repository.GetAllActiveAsync();
        return Ok(schedules);
    }

    /// <summary>
    /// Create a new fee schedule.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateFeeScheduleDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>
    /// Get a single fee schedule by id.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FeeScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var schedule = await _repository.GetByIdAsync(id);
        if (schedule == null) return NotFound();
        return Ok(schedule);
    }

    /// <summary>
    /// Update an existing fee schedule.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFeeScheduleDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }

    /// <summary>
    /// Archive (soft-delete) a fee schedule by setting IsArchived to true.
    /// </summary>
    [HttpPost("{id:int}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.ArchiveAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Permanently delete a fee schedule.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.DeleteAsync(id);
        return NoContent();
    }
}

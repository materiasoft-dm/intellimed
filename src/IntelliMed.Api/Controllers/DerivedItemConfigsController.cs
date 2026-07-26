using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/derived-item-configs")]
public class DerivedItemConfigsController : ControllerBase
{
    private readonly IDerivedItemConfigRepository _repository;

    public DerivedItemConfigsController(IDerivedItemConfigRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// List every derived-item rule (global catalog, not clinic-scoped).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DerivedItemConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var configs = await _repository.GetAllAsync();
        return Ok(configs);
    }

    /// <summary>
    /// Get a single derived-item rule by id.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DerivedItemConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var config = await _repository.GetByIdAsync(id);
        if (config == null) return NotFound();
        return Ok(config);
    }

    /// <summary>
    /// Create a new derived-item rule for a billing item.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDerivedItemConfigDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>
    /// Update an existing derived-item rule.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDerivedItemConfigDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }

    /// <summary>
    /// Delete a derived-item rule.
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

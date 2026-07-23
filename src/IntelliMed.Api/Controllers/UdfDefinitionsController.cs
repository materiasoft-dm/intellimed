using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Clinic-wide user-defined field definitions (the schema of custom fields available to patient records).
/// </summary>
[ApiController]
[Route("api/udf-definitions")]
public class UdfDefinitionsController : ControllerBase
{
    private readonly IUserDefinedFieldTypeRepository _repository;

    public UdfDefinitionsController(IUserDefinedFieldTypeRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserDefinedFieldTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActive()
    {
        var fieldTypes = await _repository.GetAllActiveAsync();
        return Ok(fieldTypes);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateUserDefinedFieldTypeDto dto)
    {
        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAllActive), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDefinedFieldTypeDto dto)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }
}

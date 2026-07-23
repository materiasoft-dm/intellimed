using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly IPatientRepository _patientRepository;

    public PatientsController(IPatientRepository patientRepository)
    {
        _patientRepository = patientRepository;
    }

    /// <summary>
    /// Search patients with optional query and active filter, returning paged results.
    /// </summary>
    /// <param name="search">Search parameters (query, isActive, page, pageSize).</param>
    /// <returns>Paged list of patients and total count.</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<PatientDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] PatientSearchDto search)
    {
        var (items, totalCount) = await _patientRepository.GetPagedAsync(search);
        return Ok(new PagedResult<PatientDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = search.Page,
            PageSize = search.PageSize
        });
    }

    /// <summary>
    /// Create a new patient.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePatientDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var id = await _patientRepository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>
    /// Get a single patient by id.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var patient = await _patientRepository.GetByIdAsync(id);
        if (patient == null) return NotFound();
        return Ok(patient);
    }

    /// <summary>
    /// Update an existing patient.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePatientDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _patientRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _patientRepository.UpdateAsync(id, dto);
        return NoContent();
    }

    /// <summary>
    /// Archive (soft-delete) a patient by setting IsActive to false.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id)
    {
        var existing = await _patientRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _patientRepository.ArchiveAsync(id);
        return NoContent();
    }
}

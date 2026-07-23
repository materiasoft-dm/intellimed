using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId:int}/family")]
public class PatientFamilyController : ControllerBase
{
    private readonly IPatientFamilyRelationshipRepository _repository;

    public PatientFamilyController(IPatientFamilyRelationshipRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PatientFamilyRelationshipDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPatient(int patientId)
    {
        var relationships = await _repository.GetByPatientIdAsync(patientId);
        return Ok(relationships);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(int patientId, [FromBody] CreatePatientFamilyRelationshipDto dto)
    {
        dto.PatientId = patientId;
        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetByPatient), new { patientId }, new { id });
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int patientId, int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null || existing.PatientId != patientId) return NotFound();

        await _repository.DeleteAsync(id);
        return NoContent();
    }
}

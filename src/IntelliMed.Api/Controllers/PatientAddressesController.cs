using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId:int}/addresses")]
public class PatientAddressesController : ControllerBase
{
    private readonly IPatientAddressRepository _repository;

    public PatientAddressesController(IPatientAddressRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PatientAddressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPatient(int patientId)
    {
        var addresses = await _repository.GetByPatientIdAsync(patientId);
        return Ok(addresses);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(int patientId, [FromBody] CreatePatientAddressDto dto)
    {
        dto.PatientId = patientId;
        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetByPatient), new { patientId }, new { id });
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int patientId, int id, [FromBody] UpdatePatientAddressDto dto)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null || existing.PatientId != patientId) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
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

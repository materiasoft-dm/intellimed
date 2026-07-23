using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId:int}/referrals")]
public class PatientReferralsController : ControllerBase
{
    private readonly IPatientReferralRepository _repository;

    public PatientReferralsController(IPatientReferralRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PatientReferralDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPatient(int patientId)
    {
        var referrals = await _repository.GetByPatientIdAsync(patientId);
        return Ok(referrals);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(int patientId, [FromBody] CreatePatientReferralDto dto)
    {
        dto.PatientId = patientId;
        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetByPatient), new { patientId }, new { id });
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int patientId, int id, [FromBody] UpdatePatientReferralDto dto)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null || existing.PatientId != patientId) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }

    [HttpPost("{id:int}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int patientId, int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null || existing.PatientId != patientId) return NotFound();

        await _repository.ArchiveAsync(id);
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

using IntelliMed.Api.Authorization;
using System.Security.Claims;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Client-scoped prescribing: list/create/update/cease/cancel/issue, plus the wizard's live safety
/// check. No Delete — see Prescription.cs. Mutating actions are gated by a real HasAccessAsync() check
/// (not just RolePermissions-as-nav-filter, the convention most other client-child controllers use) —
/// a deliberate deviation given this produces a legal prescription document.
/// </summary>
[ApiController]
[Route("api/clients/{clientId:int}/prescriptions")]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionRepository _prescriptionRepository;
    private readonly IPrescriptionIssueRepository _issueRepository;
    private readonly IMedicineRepository _medicineRepository;
    private readonly IMedicationInteractionChecker _safetyChecker;
    private readonly IProgressNoteRepository _progressNoteRepository;
    private readonly AppDbContext _context;

    public PrescriptionsController(
        IPrescriptionRepository prescriptionRepository,
        IPrescriptionIssueRepository issueRepository,
        IMedicineRepository medicineRepository,
        IMedicationInteractionChecker safetyChecker,
        IProgressNoteRepository progressNoteRepository,
        AppDbContext context)
    {
        _prescriptionRepository = prescriptionRepository;
        _issueRepository = issueRepository;
        _medicineRepository = medicineRepository;
        _safetyChecker = safetyChecker;
        _progressNoteRepository = progressNoteRepository;
        _context = context;
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<PrescriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByClient(int clientId, [FromQuery] bool includeInactive = false)
    {
        if (!await HasAccessAsync("prescriptions")) return Forbid();

        var prescriptions = await _prescriptionRepository.GetByClientIdAsync(clientId, includeInactive);
        return Ok(prescriptions);
    }

    [HttpPost("check-safety")]
    [Authorize]
    [ProducesResponseType(typeof(MedicationSafetyCheckResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckSafety(int clientId, [FromBody] CheckPrescriptionSafetyDto dto)
    {
        if (!await HasAccessAsync("prescriptions/create")) return Forbid();

        var candidateIngredientIds = new List<int>();
        if (dto.MedicineId.HasValue)
        {
            var medicine = await _medicineRepository.GetByIdAsync(dto.MedicineId.Value);
            if (medicine != null)
                candidateIngredientIds = medicine.ActiveIngredients.Select(i => i.Id).ToList();
        }

        var existingIngredients = await _prescriptionRepository.GetActiveIngredientsForClientAsync(clientId);

        var result = await _safetyChecker.CheckAsync(new MedicationSafetyCheckRequest(
            clientId, dto.MedicineName, dto.GenericName, candidateIngredientIds, existingIngredients,
            dto.MedicineId));

        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(int clientId, [FromBody] CreatePrescriptionDto dto)
    {
        if (!await HasAccessAsync("prescriptions/create")) return Forbid();

        dto.ClientId = clientId;
        var id = await _prescriptionRepository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetByClient), new { clientId }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int clientId, int id, [FromBody] UpdatePrescriptionDto dto)
    {
        if (!await HasAccessAsync("prescriptions/edit")) return Forbid();

        var existing = await _prescriptionRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _prescriptionRepository.UpdateAsync(id, dto);
        return NoContent();
    }

    [HttpPost("{id:int}/cease")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cease(int clientId, int id)
    {
        if (!await HasAccessAsync("prescriptions/edit")) return Forbid();

        var existing = await _prescriptionRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _prescriptionRepository.CeaseAsync(id);
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int clientId, int id)
    {
        if (!await HasAccessAsync("prescriptions/edit")) return Forbid();

        var existing = await _prescriptionRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _prescriptionRepository.CancelAsync(id);
        return NoContent();
    }

    [HttpGet("{id:int}/issues")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<PrescriptionIssueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIssues(int clientId, int id)
    {
        if (!await HasAccessAsync("prescriptions")) return Forbid();

        var existing = await _prescriptionRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        var issues = await _issueRepository.GetByPrescriptionIdAsync(id);
        return Ok(issues);
    }

    [HttpPost("{id:int}/issue")]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Issue(int clientId, int id, [FromBody] IssuePrescriptionDto dto)
    {
        if (!await HasAccessAsync("prescriptions/edit")) return Forbid();

        var existing = await _prescriptionRepository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        var issueId = await _issueRepository.IssueAsync(id, dto);

        // Auto-log into the open Progress Note for this encounter, if there is one — mirrors a real
        // Medinet convention (see the "Cross-module integration" section of the Progress Notes plan),
        // done here with an actual Appointment-FK lookup rather than session coincidence. No-ops if
        // this prescription has no linked Appointment, or that Appointment has no note open.
        if (existing.AppointmentId.HasValue)
        {
            var summaryParts = new List<string> { existing.MedicineName };
            if (!string.IsNullOrWhiteSpace(existing.Dose)) summaryParts.Add(existing.Dose);
            if (!string.IsNullOrWhiteSpace(existing.Frequency)) summaryParts.Add(existing.Frequency);
            var summary = $"Prescribed: {string.Join(" ", summaryParts)}";

            await _progressNoteRepository.AppendSystemEntryAsync(
                existing.AppointmentId.Value, summary, "Prescription", id, dto.IssuedByPractitionerId);
        }

        return CreatedAtAction(nameof(GetIssues), new { clientId, id }, new { issueId });
    }

    private Task<bool> HasAccessAsync(string pageKey) =>
        RolePermissions.HasAccessAsync(_context, User, pageKey);
}

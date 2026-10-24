using System.Security.Claims;
using IntelliMed.Api.Authorization;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// The results inbox: pathology and imaging reports received from labs, waiting for a doctor to
/// acknowledge them.
///
/// Everything here is gated on the "results" page key and scoped to the caller's selected clinic
/// (X-Clinic-Id), which the caller must belong to. Results are sensitive: a report is never returned
/// for a clinic the caller is not a member of, and the only way a report leaves the inbox is an
/// explicit acknowledgement that records who and when.
/// </summary>
[ApiController]
[Route("api/results")]
[Authorize]
public class ResultsController : ControllerBase
{
    private const string PageKey = "results";
    private const long MaxUploadBytes = 25 * 1024 * 1024;

    private readonly IResultRepository _repository;
    private readonly IResultIngestService _ingest;
    private readonly AppDbContext _context;

    public ResultsController(IResultRepository repository, IResultIngestService ingest, AppDbContext context)
    {
        _repository = repository;
        _ingest = ingest;
        _context = context;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private int? RequestedClinicId =>
        Request.Headers.TryGetValue("X-Clinic-Id", out var value) && int.TryParse(value, out var id) ? id : null;

    private bool IsAdmin => RolePermissions.RolesOf(User).Any(r => r is "SuperAdmin" or "Admin");

    /// <summary>The caller's selected clinic, only if they may work in it. Null → respond 403/400.</summary>
    private async Task<int?> ResolveClinicAsync()
    {
        var isSuperAdmin = RolePermissions.RolesOf(User).Contains(RolePermissions.AllAccessRole);
        var clinicId = RequestedClinicId;

        if (clinicId == null)
        {
            // No clinic selected yet. SuperAdmin falls back to the default clinic, as the other
            // clinic-scoped controllers do; anyone else only if they belong to exactly one clinic.
            if (isSuperAdmin) return 1;
            var mine = await _context.UserClinics
                .Where(uc => uc.ApplicationUserId == UserId)
                .Select(uc => uc.ClinicId)
                .Take(2)
                .ToListAsync();
            return mine.Count == 1 ? mine[0] : null;
        }

        if (isSuperAdmin) return clinicId;

        var member = await _context.UserClinics.AnyAsync(uc => uc.ApplicationUserId == UserId && uc.ClinicId == clinicId.Value);
        return member ? clinicId : null;
    }

    private Task<bool> HasAccessAsync() => RolePermissions.HasAccessAsync(_context, User, PageKey);

    /// <summary>The practitioner this login is linked to, used to narrow the inbox to "mine".</summary>
    private Task<int?> MyPractitionerIdAsync() =>
        _context.Practitioners
            .Where(p => p.ApplicationUserId == UserId && p.IsActive)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync();

    /// <summary>Loads a report's clinic and checks the caller may act on it. Returns an error result or null.</summary>
    private async Task<IActionResult?> CheckReportAccessAsync(int id)
    {
        if (!await HasAccessAsync()) return Forbid();
        var clinicId = await ResolveClinicAsync();
        if (clinicId == null) return BadRequest("Select a clinic you belong to first.");

        var reportClinic = await _repository.GetClinicIdAsync(id);
        if (reportClinic == null || reportClinic != clinicId) return NotFound();
        return null;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ResultSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] ResultStatus? status, [FromQuery] string? query, [FromQuery] bool abnormalOnly = false,
        [FromQuery] bool mine = false, [FromQuery] int? clientId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        if (!await HasAccessAsync()) return Forbid();
        var clinicId = await ResolveClinicAsync();
        if (clinicId == null) return BadRequest("Select a clinic you belong to first.");

        var result = await _repository.SearchAsync(new ResultSearchDto
        {
            ClinicId = clinicId,
            Status = status,
            Query = query,
            AbnormalOnly = abnormalOnly,
            ClientId = clientId,
            PractitionerId = mine ? await MyPractitionerIdAsync() : null,
            Page = page,
            PageSize = pageSize
        });
        return Ok(result);
    }

    [HttpGet("counts")]
    [ProducesResponseType(typeof(ResultCountsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Counts([FromQuery] bool mine = false)
    {
        if (!await HasAccessAsync()) return Forbid();
        var clinicId = await ResolveClinicAsync();
        if (clinicId == null) return BadRequest("Select a clinic you belong to first.");

        return Ok(await _repository.GetCountsAsync(clinicId, mine ? await MyPractitionerIdAsync() : null));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ResultDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var denied = await CheckReportAccessAsync(id);
        if (denied != null) return denied;

        var report = await _repository.GetByIdAsync(id);
        return report == null ? NotFound() : Ok(report);
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> GetAttachment(int id, int attachmentId)
    {
        var denied = await CheckReportAccessAsync(id);
        if (denied != null) return denied;

        var attachment = await _repository.GetAttachmentAsync(id, attachmentId);
        if (attachment == null) return NotFound();
        return File(attachment.Data, attachment.ContentType, attachment.FileName);
    }

    /// <summary>Upload an HL7 result file from the browser — the same pipeline the ingest endpoint uses.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(IngestResultOutcomeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (!await HasAccessAsync()) return Forbid();
        var clinicId = await ResolveClinicAsync();
        if (clinicId == null) return BadRequest("Select a clinic you belong to first.");
        if (file == null || file.Length == 0) return BadRequest("Choose an HL7 file to upload.");
        if (file.Length > MaxUploadBytes) return BadRequest("The file is too large.");

        using var reader = new StreamReader(file.OpenReadStream());
        var text = await reader.ReadToEndAsync();

        try
        {
            return Ok(await _ingest.IngestAsync(text, clinicId.Value));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id:int}/match")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Match(int id, [FromBody] MatchResultDto dto)
    {
        var denied = await CheckReportAccessAsync(id);
        if (denied != null) return denied;

        return await _repository.MatchAsync(id, dto.ClientId, dto.RememberLabIdentifier, UserId)
            ? NoContent()
            : BadRequest("The report could not be matched to that client.");
    }

    [HttpPost("{id:int}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Acknowledge(int id, [FromBody] AcknowledgeResultDto dto)
    {
        var denied = await CheckReportAccessAsync(id);
        if (denied != null) return denied;

        return await _repository.AcknowledgeAsync(id, dto.Comment, UserId)
            ? NoContent()
            : BadRequest("Only a report filed under a patient can be acknowledged.");
    }

    [HttpPost("{id:int}/hold")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Hold(int id, [FromBody] HoldResultDto dto)
    {
        var denied = await CheckReportAccessAsync(id);
        if (denied != null) return denied;

        return await _repository.HoldAsync(id, dto.Note) ? NoContent() : BadRequest("Only an inbox report can be held.");
    }

    [HttpPost("{id:int}/reopen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reopen(int id)
    {
        var denied = await CheckReportAccessAsync(id);
        if (denied != null) return denied;

        return await _repository.ReopenAsync(id) ? NoContent() : BadRequest("Only a held or archived report can be reopened.");
    }

    // ---- ingest keys (Admin / SuperAdmin) ----

    [HttpGet("ingest-keys")]
    [ProducesResponseType(typeof(List<ResultIngestKeyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIngestKeys()
    {
        if (!IsAdmin) return Forbid();
        var clinicId = await ResolveClinicAsync();
        if (clinicId == null) return BadRequest("Select a clinic you belong to first.");
        return Ok(await _repository.GetIngestKeysAsync(clinicId));
    }

    [HttpPost("ingest-keys")]
    [ProducesResponseType(typeof(CreatedResultIngestKeyDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateIngestKey([FromBody] CreateResultIngestKeyDto dto)
    {
        if (!IsAdmin) return Forbid();
        var clinicId = await ResolveClinicAsync();
        if (clinicId == null) return BadRequest("Select a clinic you belong to first.");
        if (string.IsNullOrWhiteSpace(dto.Label)) return BadRequest("Give the key a label, e.g. the lab or relay it is for.");

        var created = await _repository.CreateIngestKeyAsync(clinicId.Value, dto.Label);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpDelete("ingest-keys/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeIngestKey(int id)
    {
        if (!IsAdmin) return Forbid();
        var clinicId = await ResolveClinicAsync();
        if (clinicId == null) return BadRequest("Select a clinic you belong to first.");

        // Only keys belonging to the caller's clinic may be revoked.
        var keys = await _repository.GetIngestKeysAsync(clinicId);
        if (keys.All(k => k.Id != id)) return NotFound();

        return await _repository.RevokeIngestKeyAsync(id) ? NoContent() : NotFound();
    }
}

/// <summary>
/// Machine-to-machine entry point for lab results: a vendor feed or a clinic-side relay POSTs the raw
/// HL7 v2 message with an ingest key. Unauthenticated by login, authenticated by the key, which is
/// bound to one clinic — so a key can only ever deliver into its own clinic's inbox.
/// </summary>
[ApiController]
[Route("api/results/ingest")]
[AllowAnonymous]
public class ResultIngestController : ControllerBase
{
    private const long MaxMessageBytes = 25 * 1024 * 1024;

    private readonly IResultRepository _repository;
    private readonly IResultIngestService _ingest;

    public ResultIngestController(IResultRepository repository, IResultIngestService ingest)
    {
        _repository = repository;
        _ingest = ingest;
    }

    [HttpPost]
    [RequestSizeLimit(MaxMessageBytes)]
    [ProducesResponseType(typeof(IngestResultOutcomeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Ingest()
    {
        var key = Request.Headers["X-Api-Key"].ToString();
        var clinicId = await _repository.ResolveIngestKeyAsync(key);
        if (clinicId == null) return Unauthorized();

        using var reader = new StreamReader(Request.Body);
        var message = await reader.ReadToEndAsync();

        try
        {
            return Ok(await _ingest.IngestAsync(message, clinicId.Value));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

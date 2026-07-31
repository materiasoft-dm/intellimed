using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Admin CRUD for clinic-authored email templates, and their assignment to fixed system events
/// (invite email, forgot password) that other controllers resolve when sending those emails.
/// </summary>
[ApiController]
[Route("api/email-templates")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class EmailTemplatesController : ControllerBase
{
    private readonly IEmailTemplateRepository _repository;
    private readonly ILogger<EmailTemplatesController> _logger;

    public EmailTemplatesController(IEmailTemplateRepository repository, ILogger<EmailTemplatesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<EmailTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _repository.GetAllAsync(GetCurrentClinicId()));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmailTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var template = await _repository.GetByIdAsync(id);
        if (template == null) return NotFound();
        return Ok(template);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SaveEmailTemplateRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var id = await _repository.CreateAsync(GetCurrentClinicId(), request);
        _logger.LogInformation("Email template '{Name}' (id {Id}) created by {User}, event: {EventKey}",
            request.Name, id, User.Identity?.Name, request.EventKey ?? "(unassigned)");
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveEmailTemplateRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(id, request);
        _logger.LogInformation("Email template '{Name}' (id {Id}) updated by {User}, event: {EventKey}",
            request.Name, id, User.Identity?.Name, request.EventKey ?? "(unassigned)");
        return NoContent();
    }

    [HttpPost("{id:int}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.ArchiveAsync(id);
        _logger.LogInformation("Email template '{Name}' (id {Id}) archived by {User}", existing.Name, id, User.Identity?.Name);
        return NoContent();
    }

    /// <summary>Image upload target for the template body's rich-text editor. Returns a self-contained base64 data URI — no file storage needed.</summary>
    [HttpPost("upload-image")]
    [RequestSizeLimit(5_000_000)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage()
    {
        var file = Request.Form.Files.FirstOrDefault();
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var contentType = string.IsNullOrEmpty(file.ContentType) ? "image/png" : file.ContentType;

        return Ok(new { Url = $"data:{contentType};base64,{base64}" });
    }

    private int GetCurrentClinicId()
    {
        if (Request.Headers.TryGetValue("X-Clinic-Id", out var value) &&
            int.TryParse(value.ToString(), out var clinicId))
        {
            return clinicId;
        }
        return 1;
    }
}

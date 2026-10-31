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
/// Flat route (not nested under a client/prescription) so PrescriptionPrint.tsx can fetch a
/// complete, ready-to-render script from just an issue id, without threading clientId/prescriptionId
/// through its own route. Get only — see PrescriptionIssue.cs for why there's no Update/Delete.
/// </summary>
[ApiController]
[Route("api/prescription-issues")]
public class PrescriptionIssuesController : ControllerBase
{
    private const string PageKey = "prescriptions";

    private readonly IPrescriptionIssueRepository _repository;
    private readonly AppDbContext _context;

    public PrescriptionIssuesController(IPrescriptionIssueRepository repository, AppDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    [HttpGet("{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(PrescriptionIssueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await HasAccessAsync()) return Forbid();

        var issue = await _repository.GetByIdAsync(id);
        return issue == null ? NotFound() : Ok(issue);
    }

    private Task<bool> HasAccessAsync() =>
        RolePermissions.HasAccessAsync(_context, User, PageKey);
}

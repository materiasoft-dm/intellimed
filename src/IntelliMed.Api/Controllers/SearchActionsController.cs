using IntelliMed.Api.Authorization;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Catalogue of entries for the global command palette (floating search button). Reading the
/// catalogue is open to any authenticated user — the client fetches it once and caches it, then
/// filters client-side by the caller's own accessible pages.
///
/// Managing entries is gated on the "admin/search-actions" page key, so the toggle at /admin/roles
/// actually controls it — these actions previously carried [Authorize(Roles = "SuperAdmin,Admin")]
/// and ignored the catalogue.
/// </summary>
[ApiController]
[Route("api/search-actions")]
[Authorize]
public class SearchActionsController : ControllerBase
{
    private const string PageKey = "admin/search-actions";

    private readonly ISearchActionRepository _repository;
    private readonly AppDbContext _context;

    public SearchActionsController(ISearchActionRepository repository, AppDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    private Task<bool> HasAccessAsync() => RolePermissions.HasAccessAsync(_context, User, PageKey);

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SearchActionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActive()
    {
        return Ok(await _repository.GetAllActiveAsync());
    }

    [HttpGet("{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(SearchActionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await HasAccessAsync()) return Forbid();

        var action = await _repository.GetByIdAsync(id);
        if (action == null) return NotFound();
        return Ok(action);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SaveSearchActionRequest request)
    {
        if (!await HasAccessAsync()) return Forbid();

        if (!ModelState.IsValid) return BadRequest(ModelState);

        var id = await _repository.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveSearchActionRequest request)
    {
        if (!await HasAccessAsync()) return Forbid();

        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(id, request);
        return NoContent();
    }

    [HttpPost("{id:int}/archive")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id)
    {
        if (!await HasAccessAsync()) return Forbid();

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.ArchiveAsync(id);
        return NoContent();
    }
}

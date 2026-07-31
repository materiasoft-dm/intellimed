using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Catalogue of entries for the global command palette (floating search button). Reading the
/// catalogue is open to any authenticated user — the client fetches it once and caches it, then
/// filters client-side by the caller's own accessible pages. Managing entries is admin-only.
/// </summary>
[ApiController]
[Route("api/search-actions")]
[Authorize]
public class SearchActionsController : ControllerBase
{
    private readonly ISearchActionRepository _repository;

    public SearchActionsController(ISearchActionRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SearchActionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActive()
    {
        return Ok(await _repository.GetAllActiveAsync());
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(SearchActionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var action = await _repository.GetByIdAsync(id);
        if (action == null) return NotFound();
        return Ok(action);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SaveSearchActionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var id = await _repository.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveSearchActionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(id, request);
        return NoContent();
    }

    [HttpPost("{id:int}/archive")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.ArchiveAsync(id);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly IClientRepository _clientRepository;

    public ClientsController(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    /// <summary>
    /// Search clients with optional query and active filter, returning paged results.
    /// </summary>
    /// <param name="search">Search parameters (query, isActive, page, pageSize).</param>
    /// <returns>Paged list of clients and total count.</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<ClientDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] ClientSearchDto search)
    {
        search.ClinicId = GetCurrentClinicId();
        var (items, totalCount) = await _clientRepository.GetPagedAsync(search);
        return Ok(new PagedResult<ClientDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = search.Page,
            PageSize = search.PageSize
        });
    }

    /// <summary>
    /// Create a new client.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateClientDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        dto.ClinicId = GetCurrentClinicId() ?? 1;
        var id = await _clientRepository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>
    /// Get a single client by id.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var client = await _clientRepository.GetByIdAsync(id);
        if (client == null) return NotFound();
        return Ok(client);
    }

    /// <summary>
    /// Update an existing client.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateClientDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _clientRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _clientRepository.UpdateAsync(id, dto);
        return NoContent();
    }

    /// <summary>
    /// Archive (soft-delete) a client by setting IsActive to false.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id)
    {
        var existing = await _clientRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _clientRepository.ArchiveAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Reads the caller's currently selected clinic from the X-Clinic-Id header set by the Web client.
    /// Null/missing (e.g. existing tests that don't send it) means "no clinic filtering" for search,
    /// and defaults to clinic 1 for create.
    /// </summary>
    private int? GetCurrentClinicId()
    {
        if (Request.Headers.TryGetValue("X-Clinic-Id", out var value) &&
            int.TryParse(value.ToString(), out var clinicId))
        {
            return clinicId;
        }
        return null;
    }
}

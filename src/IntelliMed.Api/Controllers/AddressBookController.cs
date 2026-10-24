using IntelliMed.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// The practice's address book of people — GPs, specialists, allied health — shared across clinics.
///
/// Reads are open to any authenticated user because the client record's pickers (referrals, and
/// later letters and results) search it. Changes are gated on the "address-book" page key, which
/// every role holds by default so front-desk staff can add a referrer while booking.
/// </summary>
[ApiController]
[Route("api/address-book")]
[Authorize]
public class AddressBookController : ControllerBase
{
    private const string PageKey = "address-book";

    private readonly IAddressBookRepository _repository;
    private readonly AppDbContext _context;

    public AddressBookController(IAddressBookRepository repository, AppDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    private Task<bool> HasAccessAsync() => RolePermissions.HasAccessAsync(_context, User, PageKey);

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AddressBookContactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] AddressBookSearchDto search)
    {
        return Ok(await _repository.SearchAsync(search));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AddressBookContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var contact = await _repository.GetByIdAsync(id);
        if (contact == null) return NotFound();
        return Ok(contact);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SaveAddressBookContactDto dto)
    {
        if (!await HasAccessAsync()) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveAddressBookContactDto dto)
    {
        if (!await HasAccessAsync()) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (await _repository.GetByIdAsync(id) == null) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }

    /// <summary>Archive rather than delete — referrals and letters keep pointing at the contact.</summary>
    [HttpPost("{id:int}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id)
    {
        if (!await HasAccessAsync()) return Forbid();
        if (await _repository.GetByIdAsync(id) == null) return NotFound();

        await _repository.ArchiveAsync(id);
        return NoContent();
    }
}

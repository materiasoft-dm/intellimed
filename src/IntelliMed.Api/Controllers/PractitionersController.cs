using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Read-only lookup of practitioners, used to populate the Provider dropdown on invoices.
/// </summary>
[ApiController]
[Route("api/practitioners")]
public class PractitionersController : ControllerBase
{
    private readonly IPractitionerRepository _repository;

    public PractitionersController(IPractitionerRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<PractitionerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActive()
    {
        var practitioners = await _repository.GetAllActiveAsync();
        return Ok(practitioners);
    }
}

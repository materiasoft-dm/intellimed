using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Read-only lookup of health funds, used to populate the Fund Id dropdown on patient records/search.
/// </summary>
[ApiController]
[Route("api/health-funds")]
public class HealthFundsController : ControllerBase
{
    private readonly IHealthFundRepository _repository;

    public HealthFundsController(IHealthFundRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<HealthFundDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActive()
    {
        var funds = await _repository.GetAllActiveAsync();
        return Ok(funds);
    }
}

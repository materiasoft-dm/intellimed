using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Read-only lookup of provider groups, used to populate the Group dropdown on the profile settings page.
/// </summary>
[ApiController]
[Route("api/provider-groups")]
public class ProviderGroupsController : ControllerBase
{
    private readonly IProviderGroupRepository _repository;

    public ProviderGroupsController(IProviderGroupRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProviderGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActive()
    {
        var groups = await _repository.GetAllActiveAsync();
        return Ok(groups);
    }
}

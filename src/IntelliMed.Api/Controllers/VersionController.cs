using System.Reflection;
using IntelliMed.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Reports the deployed build's version — read from the assembly's AssemblyVersion (set via
/// IntelliMed.Api.csproj's &lt;Version&gt;, bumped by hand before every publish). Lets the UI (and
/// anyone checking directly) tell a fresh deploy apart from a stale one without hunting through the nav.
/// </summary>
[ApiController]
[Route("api/version")]
[AllowAnonymous]
public class VersionController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(VersionDto), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        return Ok(new VersionDto { Version = version });
    }
}

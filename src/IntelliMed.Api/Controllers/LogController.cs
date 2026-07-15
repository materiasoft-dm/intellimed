using Microsoft.AspNetCore.Mvc;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Receives error logs from the Blazor WASM front-end.
/// Logs them server-side for debugging and monitoring.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LogController : ControllerBase
{
    private readonly ILogger<LogController> _logger;

    public LogController(ILogger<LogController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Accepts a front-end error log entry.
    /// </summary>
    [HttpPost("frontend-error")]
    public IActionResult PostFrontendError([FromBody] FrontendErrorLogDto log)
    {
        if (log is null)
            return BadRequest("Log entry is required.");

        _logger.LogError(
            "[FRONTEND ERROR] {Timestamp:O} | Context: {Context} | Source: {Source} | {Message}\n" +
            "URL: {Url} | UA: {UserAgent}\n" +
            "StackTrace:\n{StackTrace}",
            log.Timestamp,
            log.Context ?? "N/A",
            log.Source ?? "N/A",
            log.Message ?? "N/A",
            log.Url ?? "N/A",
            log.UserAgent ?? "N/A",
            log.StackTrace ?? "N/A");

        return Ok(new { received = true });
    }
}

/// <summary>
/// DTO matching the front-end error log payload.
/// </summary>
public class FrontendErrorLogDto
{
    public string? Message { get; set; }
    public string? StackTrace { get; set; }
    public string? Source { get; set; }
    public string? Context { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? UserAgent { get; set; }
    public string? Url { get; set; }
}
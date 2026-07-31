using System.Security.Claims;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _repository;

    public NotificationsController(INotificationRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine([FromQuery] int take = 20)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        return Ok(await _repository.GetMyRecentAsync(userId, take));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var count = await _repository.GetUnreadCountAsync(userId);
        return Ok(new UnreadCountDto { Count = count });
    }

    [HttpPut("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var found = await _repository.MarkReadAsync(id, userId);
        return found ? NoContent() : NotFound();
    }

    [HttpPut("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var count = await _repository.MarkAllReadAsync(userId);
        return Ok(new { count });
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

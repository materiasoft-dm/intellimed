using System.Security.Claims;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Read-only MBS billing-item catalog, used to populate the item picker when adding invoice lines,
/// plus an admin-triggered sync from the free mbsonline.gov.au quarterly XML feed.
/// </summary>
[ApiController]
[Route("api/billing-items")]
public class BillingItemsController : ControllerBase
{
    private const string SyncPageKey = "clinic-settings";

    private readonly IBillingItemRepository _repository;
    private readonly IBillingItemSyncService _syncService;
    private readonly AppDbContext _context;

    public BillingItemsController(IBillingItemRepository repository, IBillingItemSyncService syncService, AppDbContext context)
    {
        _repository = repository;
        _syncService = syncService;
        _context = context;
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<BillingItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? query)
    {
        var items = await _repository.SearchAsync(query ?? string.Empty);
        return Ok(items);
    }

    [HttpPost("sync")]
    [Authorize]
    [ProducesResponseType(typeof(BillingItemSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Sync()
    {
        if (!await HasAccessAsync()) return Forbid();

        var result = await _syncService.SyncAsync();
        return Ok(result);
    }

    private async Task<bool> HasAccessAsync()
    {
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        if (roles.Contains("SuperAdmin")) return true;

        return await _context.RolePermissions
            .AnyAsync(p => roles.Contains(p.RoleName) && p.PageKey == SyncPageKey);
    }
}

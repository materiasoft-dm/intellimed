using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Manages SQLite database backups: listing, on-demand ("Backup Now"), download, and the scheduled-
/// interval configuration DatabaseBackupBackgroundService reads from. Hard-restricted to
/// SuperAdmin/Admin (not the dynamic per-page RolePermissions check other admin pages use) because a
/// backup is a full copy of every patient/billing record in the system — too sensitive to leave to a
/// role that could otherwise be granted just the "admin/database-backups" nav entry by mistake.
/// </summary>
[ApiController]
[Route("api/database-backups")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class DatabaseBackupsController : ControllerBase
{
    private readonly IDatabaseBackupSettingsRepository _settingsRepository;
    private readonly IDatabaseBackupService _backupService;

    public DatabaseBackupsController(
        IDatabaseBackupSettingsRepository settingsRepository,
        IDatabaseBackupService backupService)
    {
        _settingsRepository = settingsRepository;
        _backupService = backupService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DatabaseBackupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _backupService.GetAllAsync());
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(DatabaseBackupSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings()
    {
        return Ok(await _settingsRepository.GetSingletonAsync());
    }

    [HttpPut("settings")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateDatabaseBackupSettingsDto dto)
    {
        if (dto.IntervalValue <= 0)
            return BadRequest("Interval must be greater than zero.");

        await _settingsRepository.UpdateSingletonAsync(dto);
        return NoContent();
    }

    [HttpPost("backup-now")]
    [ProducesResponseType(typeof(DatabaseBackupDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> BackupNow()
    {
        var result = await _backupService.PerformBackupAsync(DatabaseBackupTrigger.Manual);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpGet("{fileName}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string fileName)
    {
        var file = await _backupService.GetBackupFileAsync(fileName);
        if (file == null) return NotFound();

        return File(file.Value.Content, "application/octet-stream", file.Value.FileName);
    }

    [HttpDelete("{fileName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string fileName)
    {
        await _backupService.DeleteAsync(fileName);
        return NoContent();
    }
}

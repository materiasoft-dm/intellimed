using FluentAssertions;
using IntelliMed.Api.Controllers;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace IntelliMed.Tests.Controllers;

public class DatabaseBackupsControllerTests
{
    private readonly Mock<IDatabaseBackupSettingsRepository> _settingsRepositoryMock = new();
    private readonly Mock<IDatabaseBackupService> _backupServiceMock = new();
    private readonly DatabaseBackupsController _controller;

    public DatabaseBackupsControllerTests()
    {
        _controller = new DatabaseBackupsController(_settingsRepositoryMock.Object, _backupServiceMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsBackupsFromService()
    {
        var backups = new List<DatabaseBackupDto> { new() { FileName = "a.db" } };
        _backupServiceMock.Setup(s => s.GetAllAsync()).ReturnsAsync(backups);

        var result = await _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(backups);
    }

    [Fact]
    public async Task UpdateSettings_ZeroInterval_ReturnsBadRequest()
    {
        var result = await _controller.UpdateSettings(new UpdateDatabaseBackupSettingsDto { IntervalValue = 0, IntervalUnit = BackupIntervalUnit.Days });

        result.Should().BeOfType<BadRequestObjectResult>();
        _settingsRepositoryMock.Verify(r => r.UpdateSingletonAsync(It.IsAny<UpdateDatabaseBackupSettingsDto>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSettings_ValidInterval_SavesAndReturnsNoContent()
    {
        var dto = new UpdateDatabaseBackupSettingsDto { IntervalValue = 2, IntervalUnit = BackupIntervalUnit.Hours };

        var result = await _controller.UpdateSettings(dto);

        result.Should().BeOfType<NoContentResult>();
        _settingsRepositoryMock.Verify(r => r.UpdateSingletonAsync(dto), Times.Once);
    }

    [Fact]
    public async Task BackupNow_TriggersManualBackup()
    {
        var dto = new DatabaseBackupDto { FileName = "manual.db", Trigger = DatabaseBackupTrigger.Manual };
        _backupServiceMock.Setup(s => s.PerformBackupAsync(DatabaseBackupTrigger.Manual)).ReturnsAsync(dto);

        var result = await _controller.BackupNow();

        result.Should().BeOfType<CreatedAtActionResult>();
        _backupServiceMock.Verify(s => s.PerformBackupAsync(DatabaseBackupTrigger.Manual), Times.Once);
    }

    [Fact]
    public async Task Download_UnknownFileName_ReturnsNotFound()
    {
        _backupServiceMock.Setup(s => s.GetBackupFileAsync("missing.db")).ReturnsAsync(((byte[] Content, string FileName)?)null);

        var result = await _controller.Download("missing.db");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Download_KnownFileName_ReturnsFileWithCorrectName()
    {
        var content = new byte[] { 1, 2, 3 };
        _backupServiceMock.Setup(s => s.GetBackupFileAsync("backup-5.db")).ReturnsAsync((content, "backup-5.db"));

        var result = await _controller.Download("backup-5.db");

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.FileContents.Should().BeSameAs(content);
        fileResult.FileDownloadName.Should().Be("backup-5.db");
    }

    [Fact]
    public async Task Delete_CallsServiceAndReturnsNoContent()
    {
        var result = await _controller.Delete("backup-5.db");

        result.Should().BeOfType<NoContentResult>();
        _backupServiceMock.Verify(s => s.DeleteAsync("backup-5.db"), Times.Once);
    }
}

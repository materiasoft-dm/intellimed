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
    private readonly Mock<IDatabaseBackupRepository> _repositoryMock = new();
    private readonly Mock<IDatabaseBackupSettingsRepository> _settingsRepositoryMock = new();
    private readonly Mock<IDatabaseBackupService> _backupServiceMock = new();
    private readonly DatabaseBackupsController _controller;

    public DatabaseBackupsControllerTests()
    {
        _controller = new DatabaseBackupsController(_repositoryMock.Object, _settingsRepositoryMock.Object, _backupServiceMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsBackupsFromRepository()
    {
        var backups = new List<DatabaseBackupDto> { new() { Id = 1, FileName = "a.db" } };
        _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(backups);

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
        var dto = new DatabaseBackupDto { Id = 5, FileName = "manual.db", Trigger = DatabaseBackupTrigger.Manual };
        _backupServiceMock.Setup(s => s.PerformBackupAsync(DatabaseBackupTrigger.Manual)).ReturnsAsync(dto);

        var result = await _controller.BackupNow();

        result.Should().BeOfType<CreatedAtActionResult>();
        _backupServiceMock.Verify(s => s.PerformBackupAsync(DatabaseBackupTrigger.Manual), Times.Once);
    }

    [Fact]
    public async Task Download_UnknownId_ReturnsNotFound()
    {
        _backupServiceMock.Setup(s => s.GetBackupFileAsync(999)).ReturnsAsync(((byte[] Content, string FileName)?)null);

        var result = await _controller.Download(999);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Download_KnownId_ReturnsFileWithCorrectName()
    {
        var content = new byte[] { 1, 2, 3 };
        _backupServiceMock.Setup(s => s.GetBackupFileAsync(5)).ReturnsAsync((content, "backup-5.db"));

        var result = await _controller.Download(5);

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.FileContents.Should().BeSameAs(content);
        fileResult.FileDownloadName.Should().Be("backup-5.db");
    }
}

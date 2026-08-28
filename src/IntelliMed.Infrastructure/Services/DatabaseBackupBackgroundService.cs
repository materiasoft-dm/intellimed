using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Every CheckInterval, compares the configured backup interval (DatabaseBackupSettings) against
/// LastRunAt and triggers a scheduled backup if due. Unlike FeeScheduleAutoImportBackgroundService
/// (which computes a fixed delay once at startup), this re-reads settings on every tick so a change
/// to the interval takes effect without an app restart, and LastRunAt is persisted in the DB so the
/// schedule survives a restart too. Manual "Backup Now" runs do not update LastRunAt — they're
/// on-demand and shouldn't push out the next scheduled run.
/// </summary>
public class DatabaseBackupBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    // Fallback only — the live value comes from SystemSettings.BackupRetentionDays, which is
    // editable on the System Settings screen and defaults to this same 60 days.
    private const int DefaultRetentionDays = 60;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseBackupBackgroundService> _logger;

    public DatabaseBackupBackgroundService(IServiceScopeFactory scopeFactory, ILogger<DatabaseBackupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIfDueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled database backup check failed.");
            }

            try
            {
                await DeleteExpiredAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database backup retention cleanup failed.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private async Task RunIfDueAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsRepository = scope.ServiceProvider.GetRequiredService<IDatabaseBackupSettingsRepository>();
        var settings = await settingsRepository.GetSingletonAsync();

        var interval = ToTimeSpan(settings.IntervalValue, settings.IntervalUnit);
        var isDue = settings.LastRunAt == null || DateTime.UtcNow - settings.LastRunAt.Value >= interval;
        if (!isDue) return;

        var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
        await backupService.PerformBackupAsync(DatabaseBackupTrigger.Scheduled);
        await settingsRepository.MarkRanAsync(DateTime.UtcNow);
    }

    private async Task DeleteExpiredAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
        var settingsRepository = scope.ServiceProvider.GetRequiredService<ISystemSettingsRepository>();

        var settings = await settingsRepository.GetAsync();
        var retentionDays = settings.BackupRetentionDays > 0 ? settings.BackupRetentionDays : DefaultRetentionDays;

        var deleted = await backupService.DeleteExpiredAsync(DateTime.UtcNow.AddDays(-retentionDays));
        if (deleted > 0)
        {
            _logger.LogInformation("Database backup retention cleanup: deleted {Count} backup(s) older than {Days} days.", deleted, retentionDays);
        }
    }

    private static TimeSpan ToTimeSpan(int value, BackupIntervalUnit unit) => unit switch
    {
        BackupIntervalUnit.Minutes => TimeSpan.FromMinutes(value),
        BackupIntervalUnit.Hours => TimeSpan.FromHours(value),
        BackupIntervalUnit.Days => TimeSpan.FromDays(value),
        BackupIntervalUnit.Weeks => TimeSpan.FromDays(value * 7),
        BackupIntervalUnit.Months => TimeSpan.FromDays(value * 30), // TimeSpan has no calendar-month unit — 30 days is an approximation
        _ => TimeSpan.FromDays(value)
    };
}

using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Periodically re-fetches every non-archived FeeSchedule that has a SourceUrl configured, so
/// health-fund/gap schedules refresh automatically instead of requiring a manual "Fetch Now" click.
/// One schedule failing to fetch does not stop the others.
/// </summary>
public class FeeScheduleAutoImportBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FeeScheduleAutoImportBackgroundService> _logger;

    public FeeScheduleAutoImportBackgroundService(IServiceScopeFactory scopeFactory, ILogger<FeeScheduleAutoImportBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importService = scope.ServiceProvider.GetRequiredService<IFeeScheduleImportService>();

        var scheduleIds = await context.FeeSchedules
            .Where(f => !f.IsArchived && f.SourceUrl != null && f.SourceUrl != "")
            .Select(f => f.Id)
            .ToListAsync(stoppingToken);

        foreach (var id in scheduleIds)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var result = await importService.FetchFromSourceAsync(id);
                _logger.LogInformation("Fee schedule {Id} auto-fetch: {Message}", id, result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fee schedule {Id} auto-fetch threw unexpectedly", id);
            }
        }
    }
}

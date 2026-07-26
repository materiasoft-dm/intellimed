using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Downloads a FeeSchedule's configured SourceUrl (a CSV of item number + fee, e.g. a file exported
/// from a health fund's provider portal) and upserts it onto the schedule's FeeScheduleItems. Records
/// LastFetchedAt/LastFetchResult on the schedule either way, so failures are visible in the UI rather
/// than silently retried forever.
/// </summary>
public class FeeScheduleImportService : IFeeScheduleImportService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;
    private readonly IFeeScheduleRepository _repository;

    public FeeScheduleImportService(HttpClient httpClient, AppDbContext context, IFeeScheduleRepository repository)
    {
        _httpClient = httpClient;
        _context = context;
        _repository = repository;
    }

    public async Task<FeeScheduleFetchResultDto> FetchFromSourceAsync(int feeScheduleId)
    {
        var now = DateTime.UtcNow;
        var schedule = await _context.FeeSchedules.FirstOrDefaultAsync(f => f.Id == feeScheduleId);
        if (schedule == null)
            return new FeeScheduleFetchResultDto { Success = false, Message = "Fee schedule not found.", FetchedAt = now };

        if (string.IsNullOrWhiteSpace(schedule.SourceUrl))
            return new FeeScheduleFetchResultDto { Success = false, Message = "No source URL is configured for this schedule.", FetchedAt = now };

        FeeScheduleFetchResultDto result;
        try
        {
            var downloadedText = await _httpClient.GetStringAsync(schedule.SourceUrl);

            var feesByItemNumber = AhsaXmlParser.TryParse(downloadedText, out var ahsaRecords)
                ? await ResolveAhsaFeesAsync(ahsaRecords)
                : FeeScheduleCsvParser.Parse(downloadedText);

            if (feesByItemNumber.Count == 0)
            {
                result = new FeeScheduleFetchResultDto { Success = false, Message = "Downloaded file contained no parseable item/fee rows.", FetchedAt = now };
            }
            else
            {
                var affected = await _repository.ImportFromParsedFeesAsync(feeScheduleId, feesByItemNumber);
                result = new FeeScheduleFetchResultDto
                {
                    Success = true,
                    ItemsAffected = affected,
                    Message = $"{affected} item fee(s) updated from {feesByItemNumber.Count} parsed row(s).",
                    FetchedAt = now
                };
            }
        }
        catch (Exception ex)
        {
            result = new FeeScheduleFetchResultDto { Success = false, Message = $"Fetch failed: {ex.Message}", FetchedAt = now };
        }

        schedule.LastFetchedAt = now;
        schedule.LastFetchResult = result.Message;
        await _context.SaveChangesAsync();

        return result;
    }

    /// <summary>
    /// Resolves AHSA records to a plain item-number-&gt;fee map: dollar benefits are used directly;
    /// percentage benefits are applied against our own BillingItem catalog's ScheduleFee, since AHSA's
    /// percentage rows carry no fee of their own.
    /// </summary>
    private async Task<Dictionary<string, decimal>> ResolveAhsaFeesAsync(List<AhsaXmlParser.AhsaFeeRecord> records)
    {
        var fees = new Dictionary<string, decimal>();

        var percentageItemNumbers = records
            .Where(r => r.DollarBenefit == null && r.PercentageBenefit != null)
            .Select(r => r.ItemNumber)
            .ToList();

        var scheduleFeesByItemNumber = percentageItemNumbers.Count == 0
            ? new Dictionary<string, decimal>()
            : await _context.BillingItems
                .Where(b => percentageItemNumbers.Contains(b.ItemNumber))
                .ToDictionaryAsync(b => b.ItemNumber, b => b.ScheduleFee);

        foreach (var record in records)
        {
            if (record.DollarBenefit.HasValue)
            {
                fees[record.ItemNumber] = record.DollarBenefit.Value;
            }
            else if (record.PercentageBenefit.HasValue && scheduleFeesByItemNumber.TryGetValue(record.ItemNumber, out var scheduleFee))
            {
                fees[record.ItemNumber] = scheduleFee * record.PercentageBenefit.Value / 100m;
            }
        }

        return fees;
    }
}

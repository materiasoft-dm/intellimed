using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

/// <summary>
/// Downloads and parses a FeeSchedule's configured SourceUrl (CSV of item number + fee), then
/// upserts the result onto the schedule's FeeScheduleItems. Used both by the on-demand "Fetch Now"
/// API action and by the periodic background auto-import service.
/// </summary>
public interface IFeeScheduleImportService
{
    Task<FeeScheduleFetchResultDto> FetchFromSourceAsync(int feeScheduleId);
}

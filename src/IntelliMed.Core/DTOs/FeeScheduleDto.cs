using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class FeeScheduleDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Note { get; set; }
    public bool IsHealthFund { get; set; }
    public int? HealthFundId { get; set; }
    public string? HealthFundCode { get; set; }
    public int? FeeTableId { get; set; }
    public string? FeeTableCode { get; set; }
    public RoundingTypeEnum RoundingType { get; set; }
    public string RoundingTypeName => RoundingType switch
    {
        RoundingTypeEnum.ToNearest1c => "To Nearest 1c",
        RoundingTypeEnum.ToNearest5c => "To Nearest 5c",
        _ => "Exact"
    };
    public bool IsArchived { get; set; }
    public string? SourceUrl { get; set; }
    public DateTime? LastFetchedAt { get; set; }
    public string? LastFetchResult { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateFeeScheduleDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Note { get; set; }
    public bool IsHealthFund { get; set; }
    public int? HealthFundId { get; set; }
    public int? FeeTableId { get; set; }
    public RoundingTypeEnum RoundingType { get; set; } = RoundingTypeEnum.Exact;
    public string? SourceUrl { get; set; }
}

public class UpdateFeeScheduleDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Note { get; set; }
    public bool IsHealthFund { get; set; }
    public int? HealthFundId { get; set; }
    public int? FeeTableId { get; set; }
    public RoundingTypeEnum RoundingType { get; set; }
    public bool IsArchived { get; set; }
    public string? SourceUrl { get; set; }
}

/// <summary>Result of a manual or background fetch-and-import from a FeeSchedule's SourceUrl.</summary>
public class FeeScheduleFetchResultDto
{
    public bool Success { get; set; }
    public int ItemsAffected { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; }
}

/// <summary>Result of auto-seeding the BBGP/BBO/BBI bulk-bill schedules from the MBS catalog.</summary>
public class SeedBulkBillResultDto
{
    public int SchedulesCreated { get; set; }
    public int ItemsAffected { get; set; }
}

public class FeeScheduleItemDto
{
    public int Id { get; set; }
    public int FeeScheduleId { get; set; }
    public int BillingItemId { get; set; }
    public string ItemNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>One past fee value for a FeeScheduleItem, in the order it took effect.</summary>
public class FeeScheduleItemHistoryDto
{
    public decimal Fee { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class CreateFeeScheduleItemDto
{
    public int BillingItemId { get; set; }
    public decimal Fee { get; set; }
}

public class UpdateFeeScheduleItemFeeDto
{
    public decimal Fee { get; set; }
}

public class ImportFeeScheduleItemsRequest
{
    /// <summary>Null means "the whole active MBS catalog"; otherwise another FeeSchedule's item list.</summary>
    public int? SourceFeeScheduleId { get; set; }
    public decimal Percent { get; set; }
    public decimal FlatAmount { get; set; }
}

public class FeeScheduleSearchDto
{
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IncludeArchived { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class FeeScheduleDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Note { get; set; }
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateFeeScheduleDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int? HealthFundId { get; set; }
    public int? FeeTableId { get; set; }
    public RoundingTypeEnum RoundingType { get; set; } = RoundingTypeEnum.Exact;
}

public class UpdateFeeScheduleDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int? HealthFundId { get; set; }
    public int? FeeTableId { get; set; }
    public RoundingTypeEnum RoundingType { get; set; }
    public bool IsArchived { get; set; }
}

public class FeeScheduleSearchDto
{
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IncludeArchived { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

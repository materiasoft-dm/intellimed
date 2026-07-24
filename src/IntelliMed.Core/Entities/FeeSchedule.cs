namespace IntelliMed.Core.Entities;

public class FeeSchedule
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int? HealthFundId { get; set; }
    public int? FeeTableId { get; set; }
    public RoundingTypeEnum RoundingType { get; set; } = RoundingTypeEnum.Exact;
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public HealthFund? HealthFund { get; set; }
    public FeeSchedule? FeeTable { get; set; }
}

public enum RoundingTypeEnum
{
    Exact,
    ToNearest1c,
    ToNearest5c
}

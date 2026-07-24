namespace IntelliMed.Core.DTOs;

public class BillingItemDto
{
    public int Id { get; set; }
    public string ItemNumber { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Group { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal ScheduleFee { get; set; }
    public decimal? Benefit100 { get; set; }
    public bool IsActive { get; set; }
}

public class BillingItemSyncResultDto
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Deactivated { get; set; }
    public DateTime SyncedAt { get; set; }
}

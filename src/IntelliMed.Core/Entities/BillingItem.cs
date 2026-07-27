namespace IntelliMed.Core.Entities;

public class BillingItem
{
    public int Id { get; set; }
    public string ItemNumber { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Group { get; set; }
    public string? SubGroup { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal ScheduleFee { get; set; }
    public decimal? Benefit100 { get; set; }
    public DateTime? ItemStartDate { get; set; }
    public DateTime? ItemEndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedAt { get; set; }
}

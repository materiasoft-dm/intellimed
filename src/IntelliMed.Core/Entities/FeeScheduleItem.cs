namespace IntelliMed.Core.Entities;

public class FeeScheduleItem
{
    public int Id { get; set; }
    public int FeeScheduleId { get; set; }
    public int BillingItemId { get; set; }
    public decimal Fee { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public FeeSchedule? FeeSchedule { get; set; }
    public BillingItem? BillingItem { get; set; }
}

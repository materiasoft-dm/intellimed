namespace IntelliMed.Core.Entities;

/// <summary>
/// One row per fee value a FeeScheduleItem has ever held, including its first value at creation.
/// Append-only: never updated or deleted except via cascade when the parent item is removed.
/// </summary>
public class FeeScheduleItemPriceHistory
{
    public int Id { get; set; }
    public int FeeScheduleItemId { get; set; }
    public decimal Fee { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public FeeScheduleItem? FeeScheduleItem { get; set; }
}

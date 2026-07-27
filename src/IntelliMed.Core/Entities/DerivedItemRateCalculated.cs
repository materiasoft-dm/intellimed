namespace IntelliMed.Core.Entities;

/// <summary>
/// Pre-calculated fee override for a derived item on a specific fee schedule, keyed by OrderNum
/// (patient count for NumberOfPatientsSeen, field quantity for FieldQuantity). When a matching row
/// exists it overrides the derived-item formula entirely for that count/quantity, ported from
/// legacy's DerivedItemRateCalculated table (child of ItemRate).
/// </summary>
public class DerivedItemRateCalculated
{
    public int Id { get; set; }
    public int FeeScheduleItemId { get; set; }
    public int OrderNum { get; set; }
    public decimal Fee { get; set; }

    public FeeScheduleItem? FeeScheduleItem { get; set; }
}

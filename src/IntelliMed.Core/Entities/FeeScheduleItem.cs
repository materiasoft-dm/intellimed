namespace IntelliMed.Core.Entities;

public class FeeScheduleItem
{
    public int Id { get; set; }
    public int FeeScheduleId { get; set; }
    public int BillingItemId { get; set; }
    public decimal Fee { get; set; }

    /// <summary>
    /// Per-schedule override for a derived item's gap-adjustment percentage (ported from legacy's
    /// ItemRate.MedicalGapPercent). When a derived-item formula's fee-schedule override
    /// (PercentageFromAssociatedItemFee) is NOT set for this item, and this IS set, the derived fee is
    /// multiplied by MedicalGapPercent/100 — e.g. a health fund's no-gap schedule marking this at 100
    /// leaves the fee unchanged. Mutually exclusive with PercentageFromAssociatedItemFee per formula.
    /// </summary>
    public decimal? MedicalGapPercent { get; set; }

    /// <summary>
    /// Per-schedule override of a derived item's association percentage (or, for CombinationOperations,
    /// its PercentageFromInvoiceTotal — legacy reuses this same column for both). When set, this
    /// replaces the DerivedItemConfig's default percentage for this specific fee schedule instead of
    /// applying MedicalGapPercent.
    /// </summary>
    public decimal? PercentageFromAssociatedItemFee { get; set; }

    /// <summary>Per-schedule override of a FieldQuantity/TimeDuration derived item's OverLimitQuantityPlus.</summary>
    public decimal? OverLimitQuantityPlus { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public FeeSchedule? FeeSchedule { get; set; }
    public BillingItem? BillingItem { get; set; }
    public ICollection<DerivedItemRateCalculated> DerivedItemRateCalculateds { get; set; } = new List<DerivedItemRateCalculated>();
}

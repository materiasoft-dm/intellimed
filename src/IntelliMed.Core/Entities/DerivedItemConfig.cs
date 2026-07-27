namespace IntelliMed.Core.Entities;

/// <summary>
/// Hand-maintained rule describing how one MBS item's fee is derived from other items/values on the
/// same invoice (assistant-at-surgery, anaesthesia time-loading, per-patient-seen billing, etc.),
/// rather than looked up flat from a fee schedule. There is no bulk-import source for this data
/// (unlike the MBS catalog or AHSA fee feeds) — rows are entered one at a time by an admin.
/// </summary>
public class DerivedItemConfig
{
    public int Id { get; set; }
    public int BillingItemId { get; set; }
    public DerivedCalculationType CalculationType { get; set; }

    /// <summary>The sibling item this line's fee is a percentage/loading of. Used by PercentageOfAssociatedItem and AssistanceAnaesthesia. Mutually exclusive with AssociatedGroup — set this for a fixed one-item pairing.</summary>
    public int? AssociatedBillingItemId { get; set; }

    /// <summary>
    /// Alternative to AssociatedBillingItemId: sums every sibling line whose BillingItem.Group equals
    /// this value (e.g. "T8" for MBS Operations), rather than matching one fixed item. This is what a
    /// real MBS assistant-at-surgery rule needs — item 51303's fee is 20% of the surgeon's *total*
    /// abated fee across every eligible operation performed on the same occasion, not just one procedure.
    /// IMultipleOperationRuleCalculator already runs first, so each sibling's fee is post-abatement.
    /// </summary>
    public string? AssociatedGroup { get; set; }

    /// <summary>Percentage of the associated item's fee (e.g. 20 for a 20% assistant fee).</summary>
    public decimal? Percentage { get; set; }

    /// <summary>Dollar value per unit/field/minute/patient, for the quantity-driven calculation types.</summary>
    public decimal? UnitValue { get; set; }

    /// <summary>Quantity threshold above which OverLimitUnitValue applies instead of UnitValue.</summary>
    public decimal? LimitQuantity { get; set; }

    /// <summary>Dollar value per unit beyond LimitQuantity.</summary>
    public decimal? OverLimitUnitValue { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public BillingItem? BillingItem { get; set; }
    public BillingItem? AssociatedBillingItem { get; set; }
}

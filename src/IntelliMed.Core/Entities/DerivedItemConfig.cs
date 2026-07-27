namespace IntelliMed.Core.Entities;

/// <summary>
/// Hand-maintained rule describing how one MBS item's fee is derived from other items/values on the
/// same invoice (assistant-at-surgery, anaesthesia time-loading, per-patient-seen billing, etc.),
/// rather than looked up flat from a fee schedule. Ported field-for-field from legacy Pracnet's
/// MbsDerivedItemInfo/DerivedFeeCalculator (Abaki.Billing.Bll) so the 8 supported formulas match
/// legacy exactly. There is no bulk-import source for this data — rows are entered one at a time by
/// an admin.
/// </summary>
public class DerivedItemConfig
{
    public int Id { get; set; }
    public int BillingItemId { get; set; }
    public DerivedCalculationType CalculationType { get; set; }

    /// <summary>The sibling item this line's fee is a percentage/loading of. Mutually exclusive with AssociatedGroup and AssociatedItemNumbers — set this for a fixed one-item pairing.</summary>
    public int? AssociatedBillingItemId { get; set; }

    /// <summary>
    /// Alternative to AssociatedBillingItemId: sums every sibling line whose BillingItem.Group equals
    /// this value (e.g. "T8" for MBS Operations), rather than matching one fixed item.
    /// </summary>
    public string? AssociatedGroup { get; set; }

    /// <summary>
    /// Alternative to AssociatedBillingItemId/AssociatedGroup: a comma/space-separated list of MBS item
    /// numbers, where an entry containing "-" (e.g. "25200-25205") expands to every item number in that
    /// numeric range. Parsed by ItemNumberRangeMatcher. Ported from legacy's AssociatedItems/
    /// BuildListItemNumFromString — this is what real MBS DerivedFee text like item 25030's needs
    /// ("an item range 25200-25205, plus...").
    /// </summary>
    public string? AssociatedItemNumbers { get; set; }

    /// <summary>Group A of a 4-group association set, used only by AssistanceAnaesthesia and BasicUnits. Same range syntax as AssociatedItemNumbers.</summary>
    public string? GroupAAssociatedItems { get; set; }

    /// <summary>Group B — AssistanceAnaesthesia and BasicUnits.</summary>
    public string? GroupBAssociatedItems { get; set; }

    /// <summary>Group C — AssistanceAnaesthesia and BasicUnits.</summary>
    public string? GroupCAssociatedItems { get; set; }

    /// <summary>Group D — AssistanceAnaesthesia only.</summary>
    public string? GroupDAssociatedItems { get; set; }

    /// <summary>Percentage of the associated item(s)' fee (e.g. 20 for a 20% assistant fee). Used by PercentageOfAssociatedItem, ExcisionMalignantTumour, ProcedureDiscontinued, AssistanceAnaesthesia.</summary>
    public decimal? Percentage { get; set; }

    /// <summary>Percentage of every other invoice line's fee combined (CombinationOperations only) — distinct from Percentage, which is scoped to the matched association list.</summary>
    public decimal? PercentageFromInvoiceTotal { get; set; }

    /// <summary>Configured flat base fee. NumberOfPatientsSeen path A and BasicUnits use this directly instead of an associated item's fee.</summary>
    public decimal? CalculateFromFee { get; set; }

    /// <summary>NumberOfPatientsSeen: patient-count threshold. At or below this, UnderNumOfLimitPatientPlusTotal is divided by the patient count; above it, OverNumOfLimitPatientPlus is added flat.</summary>
    public int? NumOfLimitPatient { get; set; }

    /// <summary>NumberOfPatientsSeen: total add-on divided by patient count when at/under NumOfLimitPatient.</summary>
    public decimal? UnderNumOfLimitPatientPlusTotal { get; set; }

    /// <summary>NumberOfPatientsSeen: flat add-on when over NumOfLimitPatient.</summary>
    public decimal? OverNumOfLimitPatientPlus { get; set; }

    /// <summary>FieldQuantity/TimeDuration: quantity threshold. FieldQuantity only adds the over-limit amount once quantity exceeds this; TimeDuration ignores it (always adds quantity x OverLimitQuantityPlus).</summary>
    public decimal? NumberOfLimitQuantity { get; set; }

    /// <summary>FieldQuantity/TimeDuration: dollar amount added per unit of quantity beyond (FieldQuantity) or including (TimeDuration) the limit.</summary>
    public decimal? OverLimitQuantityPlus { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public BillingItem? BillingItem { get; set; }
    public BillingItem? AssociatedBillingItem { get; set; }
}

using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class DerivedItemConfigDto
{
    public int Id { get; set; }
    public int BillingItemId { get; set; }
    public string ItemNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DerivedCalculationType CalculationType { get; set; }
    public int? AssociatedBillingItemId { get; set; }
    public string? AssociatedItemNumber { get; set; }
    public string? AssociatedDescription { get; set; }
    public string? AssociatedGroup { get; set; }
    public decimal? Percentage { get; set; }
    public decimal? UnitValue { get; set; }
    public decimal? LimitQuantity { get; set; }
    public decimal? OverLimitUnitValue { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateDerivedItemConfigDto
{
    public int BillingItemId { get; set; }
    public DerivedCalculationType CalculationType { get; set; }
    public int? AssociatedBillingItemId { get; set; }
    public string? AssociatedGroup { get; set; }
    public decimal? Percentage { get; set; }
    public decimal? UnitValue { get; set; }
    public decimal? LimitQuantity { get; set; }
    public decimal? OverLimitUnitValue { get; set; }
    public string? Notes { get; set; }
}

public class UpdateDerivedItemConfigDto
{
    public DerivedCalculationType CalculationType { get; set; }
    public int? AssociatedBillingItemId { get; set; }
    public string? AssociatedGroup { get; set; }
    public decimal? Percentage { get; set; }
    public decimal? UnitValue { get; set; }
    public decimal? LimitQuantity { get; set; }
    public decimal? OverLimitUnitValue { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

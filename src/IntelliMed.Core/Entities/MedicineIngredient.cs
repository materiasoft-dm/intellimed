namespace IntelliMed.Core.Entities;

/// <summary>Bridge between Medicine (product/brand) and ActiveIngredient, modeled on ClientProviderAccess.</summary>
public class MedicineIngredient
{
    public int Id { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public int ActiveIngredientId { get; set; }
    public ActiveIngredient? ActiveIngredient { get; set; }
}

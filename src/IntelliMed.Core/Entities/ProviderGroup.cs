namespace IntelliMed.Core.Entities;

public class ProviderGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

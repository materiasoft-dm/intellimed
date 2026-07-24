namespace IntelliMed.Core.Entities;

public class ClientUserDefinedFieldValue
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int UserDefinedFieldTypeId { get; set; }
    public string? Value { get; set; }
    public string? Note { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public UserDefinedFieldType? UserDefinedFieldType { get; set; }
}

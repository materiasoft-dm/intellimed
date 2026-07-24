using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class ClientUserDefinedFieldValueDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int UserDefinedFieldTypeId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public UdfFieldTypeEnum FieldType { get; set; }
    public string? Value { get; set; }
    public string? Note { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateClientUserDefinedFieldValueDto
{
    public int ClientId { get; set; }
    public int UserDefinedFieldTypeId { get; set; }
    public string? Value { get; set; }
    public string? Note { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateClientUserDefinedFieldValueDto
{
    public string? Value { get; set; }
    public string? Note { get; set; }
    public bool IsDefault { get; set; }
}

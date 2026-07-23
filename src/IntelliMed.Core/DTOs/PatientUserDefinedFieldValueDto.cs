using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class PatientUserDefinedFieldValueDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int UserDefinedFieldTypeId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public UdfFieldTypeEnum FieldType { get; set; }
    public string? Value { get; set; }
    public string? Note { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreatePatientUserDefinedFieldValueDto
{
    public int PatientId { get; set; }
    public int UserDefinedFieldTypeId { get; set; }
    public string? Value { get; set; }
    public string? Note { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdatePatientUserDefinedFieldValueDto
{
    public string? Value { get; set; }
    public string? Note { get; set; }
    public bool IsDefault { get; set; }
}

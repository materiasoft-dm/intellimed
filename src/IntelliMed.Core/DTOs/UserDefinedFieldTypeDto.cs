using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class UserDefinedFieldTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public UdfFieldTypeEnum FieldType { get; set; }
    public string? DefaultValue { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUserDefinedFieldTypeDto
{
    public string Name { get; set; } = string.Empty;
    public UdfFieldTypeEnum FieldType { get; set; } = UdfFieldTypeEnum.Text;
    public string? DefaultValue { get; set; }
    public int DisplayOrder { get; set; }
}

public class UpdateUserDefinedFieldTypeDto
{
    public string Name { get; set; } = string.Empty;
    public UdfFieldTypeEnum FieldType { get; set; }
    public string? DefaultValue { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

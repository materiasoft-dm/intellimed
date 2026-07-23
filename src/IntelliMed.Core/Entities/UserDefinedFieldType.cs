namespace IntelliMed.Core.Entities;

public enum UdfFieldTypeEnum
{
    Text,
    Checkbox,
    List
}

public class UserDefinedFieldType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public UdfFieldTypeEnum FieldType { get; set; } = UdfFieldTypeEnum.Text;
    public string? DefaultValue { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

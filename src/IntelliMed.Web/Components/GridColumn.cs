namespace IntelliMed.Web.Components;

/// <summary>
/// Column definition for the ResizableTable component.
/// </summary>
public class GridColumn
{
    /// <summary>Column header text.</summary>
    public string Name { get; set; } = "";

    /// <summary>Optional column ID for sorting reference.</summary>
    public string? Id { get; set; }

    /// <summary>Column width (e.g., "120px", "15%").</summary>
    public string? Width { get; set; }

    /// <summary>Whether the column is sortable.</summary>
    public bool Sortable { get; set; } = true;

    /// <summary>Name of a JS formatter function to apply.</summary>
    public string? FormatterName { get; set; }
}
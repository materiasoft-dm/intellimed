using IntelliMed.Core.Entities;

namespace IntelliMed.Infrastructure.Services;

public record ParsedDrugInteractionRow(
    string IngredientAName,
    string IngredientBName,
    InteractionSeverity Severity,
    string? Description = null,
    string? SourceDataset = null);

/// <summary>
/// CSV parser for the drug-interaction import format: a header row naming IngredientA (required),
/// IngredientB (required), Severity (Minor/Moderate/Major), Description, SourceDataset, matched
/// case-insensitively in any column order. Same comma/quoted-field handling as MedicineCsvParser.
/// </summary>
public static class DrugInteractionCsvParser
{
    public static List<ParsedDrugInteractionRow> Parse(string csvText)
    {
        var result = new List<ParsedDrugInteractionRow>();

        using var reader = new StringReader(csvText);
        var headerLine = reader.ReadLine();
        if (headerLine == null) return result;

        var headers = SplitLine(headerLine);
        var ingredientACol = FindColumn(headers, "ingredienta");
        var ingredientBCol = FindColumn(headers, "ingredientb");
        if (ingredientACol == -1 || ingredientBCol == -1)
            throw new InvalidOperationException("Could not find 'IngredientA'/'IngredientB' columns in the CSV header.");

        var severityCol = FindColumn(headers, "severity");
        var descriptionCol = FindColumn(headers, "description");
        var sourceCol = FindColumn(headers, "sourcedataset");

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = SplitLine(line);
            var ingredientA = Field(fields, ingredientACol)?.Trim();
            var ingredientB = Field(fields, ingredientBCol)?.Trim();
            if (string.IsNullOrEmpty(ingredientA) || string.IsNullOrEmpty(ingredientB)) continue;

            result.Add(new ParsedDrugInteractionRow(
                IngredientAName: ingredientA,
                IngredientBName: ingredientB,
                Severity: ParseSeverity(Field(fields, severityCol)),
                Description: NullIfEmpty(Field(fields, descriptionCol)),
                SourceDataset: NullIfEmpty(Field(fields, sourceCol))));
        }

        return result;
    }

    private static string? Field(string[] fields, int col) => col >= 0 && col < fields.Length ? fields[col].Trim() : null;
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static InteractionSeverity ParseSeverity(string? value) =>
        Enum.TryParse<InteractionSeverity>(value?.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : InteractionSeverity.Moderate;

    private static int FindColumn(string[] headers, string normalizedName)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            if (headers[i].Trim().Trim('"').Replace(" ", "").ToLowerInvariant() == normalizedName) return i;
        }
        return -1;
    }

    private static string[] SplitLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }
}

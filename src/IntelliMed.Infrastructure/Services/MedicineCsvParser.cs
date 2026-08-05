using IntelliMed.Core.Entities;

namespace IntelliMed.Infrastructure.Services;

public record ParsedMedicineRow(
    string Name,
    string? GenericName,
    string? Strength,
    string? Form,
    string? Manufacturer,
    string? ArtgId,
    bool IsPbsListed,
    MedicineSchedule? Schedule,
    IReadOnlyList<string> ActiveIngredients);

/// <summary>
/// Minimal CSV parser for the medicine reference-data import format: a header row naming any of
/// Name (required), GenericName, Strength, Form, Manufacturer, ArtgId, IsPbsListed, and
/// ActiveIngredients (semicolon-separated, for combination products), matched case-insensitively in
/// any column order. Handles comma delimiters and simple double-quoted fields, mirroring
/// FeeScheduleCsvParser. Deliberately source-agnostic — see MedicineImportService for why.
/// </summary>
public static class MedicineCsvParser
{
    public static List<ParsedMedicineRow> Parse(string csvText)
    {
        var result = new List<ParsedMedicineRow>();

        using var reader = new StringReader(csvText);
        var headerLine = reader.ReadLine();
        if (headerLine == null) return result;

        var headers = SplitLine(headerLine);
        var nameCol = FindColumn(headers, "name");
        if (nameCol == -1)
            throw new InvalidOperationException("Could not find a 'Name' column in the CSV header.");

        var genericCol = FindColumn(headers, "genericname");
        var strengthCol = FindColumn(headers, "strength");
        var formCol = FindColumn(headers, "form");
        var manufacturerCol = FindColumn(headers, "manufacturer");
        var artgCol = FindColumn(headers, "artgid");
        var pbsCol = FindColumn(headers, "ispbslisted");
        var scheduleCol = FindColumn(headers, "schedule");
        var ingredientsCol = FindColumn(headers, "activeingredients");

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = SplitLine(line);
            var name = Field(fields, nameCol)?.Trim();
            if (string.IsNullOrEmpty(name)) continue;

            var ingredients = ingredientsCol == -1
                ? new List<string>()
                : (Field(fields, ingredientsCol) ?? string.Empty)
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

            result.Add(new ParsedMedicineRow(
                Name: name,
                GenericName: NullIfEmpty(Field(fields, genericCol)),
                Strength: NullIfEmpty(Field(fields, strengthCol)),
                Form: NullIfEmpty(Field(fields, formCol)),
                Manufacturer: NullIfEmpty(Field(fields, manufacturerCol)),
                ArtgId: NullIfEmpty(Field(fields, artgCol)),
                IsPbsListed: bool.TryParse(Field(fields, pbsCol), out var pbs) && pbs,
                Schedule: ParseSchedule(Field(fields, scheduleCol)),
                ActiveIngredients: ingredients));
        }

        return result;
    }

    private static string? Field(string[] fields, int col) => col >= 0 && col < fields.Length ? fields[col].Trim() : null;
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static MedicineSchedule? ParseSchedule(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().TrimStart('S', 's');
        return normalized switch
        {
            "2" => MedicineSchedule.S2,
            "3" => MedicineSchedule.S3,
            "4" => MedicineSchedule.S4,
            "8" => MedicineSchedule.S8,
            _ => Enum.TryParse<MedicineSchedule>(value.Trim(), ignoreCase: true, out var parsed) ? parsed : null
        };
    }

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

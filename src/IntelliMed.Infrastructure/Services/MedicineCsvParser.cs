using IntelliMed.Core.Entities;

namespace IntelliMed.Infrastructure.Services;

public record ParsedMedicineRow(
    string Name,
    string? GenericName,
    string? Strength,
    string? Form,
    string? PackSize,
    string? Manufacturer,
    string? ArtgId,
    bool IsPbsListed,
    bool IsRpbsListed,
    PbsRestrictionLevel? RestrictionLevel,
    MedicineSchedule? Schedule,
    IReadOnlyList<string> ActiveIngredients,
    // Provenance — populated by sources that carry stable external IDs (PBS API Public v3 today).
    // All nullable: a row from a source without these (Wikidata, the self-compiled starter CSV) just
    // leaves them null. Stored verbatim from the source — never rewritten — so the same row can be
    // re-synced and matched against the same external record.
    string? PbsItemCode = null,
    string? AmtConceptId = null,
    string? AtcCode = null,
    // PBS prescribing limits and classification — see Medicine.cs for what each one drives.
    int? MaxRepeats = null,
    int? MaxQuantity = null,
    string? Route = null,
    bool IsSection100 = false,
    string? TherapeuticGroupId = null,
    string? TherapeuticGroupTitle = null);

/// <summary>
/// Minimal CSV parser for the medicine reference-data import format: a header row naming any of
/// Name (required), GenericName, Strength, Form, PackSize, Manufacturer, ArtgId, IsPbsListed, IsRpbsListed,
/// RestrictionLevel (Unrestricted/Restricted/AuthorityRequired), Schedule, and ActiveIngredients
/// (semicolon-separated, for combination products), matched case-insensitively in any column order.
/// Handles comma delimiters and simple double-quoted fields, mirroring FeeScheduleCsvParser.
/// Deliberately source-agnostic — see MedicineImportService for why.
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
        var packSizeCol = FindColumn(headers, "packsize");
        var manufacturerCol = FindColumn(headers, "manufacturer");
        var artgCol = FindColumn(headers, "artgid");
        var pbsCol = FindColumn(headers, "ispbslisted");
        var rpbsCol = FindColumn(headers, "isrpbslisted");
        var restrictionCol = FindColumn(headers, "restrictionlevel");
        var scheduleCol = FindColumn(headers, "schedule");
        var ingredientsCol = FindColumn(headers, "activeingredients");
        var pbsItemCodeCol = FindColumn(headers, "pbsitemcode");
        var amtConceptIdCol = FindColumn(headers, "amtconceptid");
        var atcCodeCol = FindColumn(headers, "atccode");

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
                PackSize: NullIfEmpty(Field(fields, packSizeCol)),
                Manufacturer: NullIfEmpty(Field(fields, manufacturerCol)),
                ArtgId: NullIfEmpty(Field(fields, artgCol)),
                IsPbsListed: bool.TryParse(Field(fields, pbsCol), out var pbs) && pbs,
                IsRpbsListed: bool.TryParse(Field(fields, rpbsCol), out var rpbs) && rpbs,
                RestrictionLevel: ParseRestrictionLevel(Field(fields, restrictionCol)),
                Schedule: ParseSchedule(Field(fields, scheduleCol)),
                ActiveIngredients: ingredients,
                PbsItemCode: NullIfEmpty(Field(fields, pbsItemCodeCol)),
                AmtConceptId: NullIfEmpty(Field(fields, amtConceptIdCol)),
                AtcCode: NullIfEmpty(Field(fields, atcCodeCol))));
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

    private static PbsRestrictionLevel? ParseRestrictionLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().Replace(" ", "").Replace("-", "");
        return Enum.TryParse<PbsRestrictionLevel>(normalized, ignoreCase: true, out var parsed) ? parsed : null;
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

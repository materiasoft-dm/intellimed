using System.Globalization;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Minimal CSV parser for fee-schedule downloads: expects a header row with an item-number column
/// (ItemNumber / Item Number / ItemNo / Code / Item Code) and a fee column (Fee / Amount / Rate / Fee($)),
/// matched case-insensitively. Handles comma or tab delimiters and simple double-quoted fields.
/// </summary>
public static class FeeScheduleCsvParser
{
    private static readonly string[] ItemNumberHeaders = ["itemnumber", "item number", "itemno", "item no", "code", "item code", "itemcode"];
    private static readonly string[] FeeHeaders = ["fee", "amount", "rate", "fee($)", "fee ($)", "price"];

    public static Dictionary<string, decimal> Parse(string csvText)
    {
        var result = new Dictionary<string, decimal>();

        using var reader = new StringReader(csvText);
        var headerLine = reader.ReadLine();
        if (headerLine == null) return result;

        var delimiter = headerLine.Contains('\t') ? '\t' : ',';
        var headers = SplitLine(headerLine, delimiter);

        var itemNumberCol = FindColumn(headers, ItemNumberHeaders);
        var feeCol = FindColumn(headers, FeeHeaders);
        if (itemNumberCol == -1 || feeCol == -1)
            throw new InvalidOperationException(
                $"Could not find item number / fee columns in the CSV header. Expected one of [{string.Join(", ", ItemNumberHeaders)}] and one of [{string.Join(", ", FeeHeaders)}], found: {headerLine}");

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = SplitLine(line, delimiter);
            if (itemNumberCol >= fields.Length || feeCol >= fields.Length) continue;

            var itemNumber = fields[itemNumberCol].Trim();
            if (string.IsNullOrEmpty(itemNumber)) continue;

            var feeText = fields[feeCol].Trim().TrimStart('$');
            if (!decimal.TryParse(feeText, NumberStyles.Any, CultureInfo.InvariantCulture, out var fee)) continue;

            result[itemNumber] = fee;
        }

        return result;
    }

    private static int FindColumn(string[] headers, string[] candidates)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var normalized = headers[i].Trim().Trim('"').ToLowerInvariant();
            if (candidates.Contains(normalized)) return i;
        }
        return -1;
    }

    private static string[] SplitLine(string line, char delimiter)
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
            else if (c == delimiter && !inQuotes)
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

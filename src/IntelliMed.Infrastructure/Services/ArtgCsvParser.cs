using IntelliMed.Core.Entities;

namespace IntelliMed.Infrastructure.Services;

/// <summary>One ARTG medicine row, reduced to the fields we actually enrich from. The export carries
/// 32 columns, most of them long free text (Indications, Warnings) that would bloat memory for no
/// benefit — the parser keeps only these.</summary>
public record ArtgMedicineRow(
    string ArtgId,
    string ProductName,
    MedicineSchedule? Schedule,
    string? DosageForm,
    string? Route);

/// <summary>
/// Streams the TGA "ARTG Search Visualisation Tool" export (Medicines filter) and pulls out the few
/// columns worth enriching from.
///
/// Two things about that export shape this parser:
/// - It starts with a filter banner ("Applied filters: ...") and a blank line, so the real header is
///   not row 1. The header is located by looking for the ARTG ID column rather than assuming a row.
/// - Free-text columns legitimately contain commas, quotes and newlines, so this reads character by
///   character across the whole stream rather than line by line. A line-based split silently
///   mis-aligns every column after an embedded newline.
///
/// Only the Medicines-filtered export is useful: the unfiltered one has 8 columns and none of the
/// clinical fields. See MEDICINE_DATA_ENRICHMENT.md.
/// </summary>
public static class ArtgCsvParser
{
    private const string ArtgIdHeader = "ARTG ID";

    public static IEnumerable<ArtgMedicineRow> Parse(TextReader reader)
    {
        int idIdx = -1, nameIdx = -1, schedIdx = -1, formIdx = -1, routeIdx = -1;
        var headerFound = false;

        foreach (var fields in ReadRecords(reader))
        {
            if (!headerFound)
            {
                var idPos = fields.FindIndex(f => string.Equals(f.Trim(), ArtgIdHeader, StringComparison.OrdinalIgnoreCase));
                if (idPos < 0) continue;   // still in the banner/blank rows

                idIdx = idPos;
                nameIdx = IndexOf(fields, "Product Name");
                schedIdx = IndexOf(fields, "Poison Schedule");
                formIdx = IndexOf(fields, "Dosage Form");
                routeIdx = IndexOf(fields, "Route of Administration");
                headerFound = true;
                continue;
            }

            var artgId = At(fields, idIdx);
            var name = At(fields, nameIdx);
            if (string.IsNullOrWhiteSpace(artgId) || string.IsNullOrWhiteSpace(name)) continue;

            yield return new ArtgMedicineRow(
                artgId.Trim(),
                name.Trim(),
                MapSchedule(At(fields, schedIdx)),
                Blank(At(fields, formIdx)),
                Blank(At(fields, routeIdx)));
        }
    }

    /// <summary>
    /// Maps the export's Poison Schedule text onto MedicineSchedule.
    ///
    /// A cell can name more than one schedule ("(S2) Pharmacy Medicine, (S3) Pharmacist Only
    /// Medicine") because different strengths or pack sizes of the same registration are scheduled
    /// differently. **The strictest wins**, deliberately: the prescribing wizard uses this to decide
    /// whether to demand a state authority code, and over-prompting is a far cheaper error than
    /// missing a Schedule 8 presentation.
    ///
    /// S1 is ignored — in the Poisons Standard that schedule is intentionally blank, so it carries no
    /// meaning. S5/S6/S7/S9 have no MedicineSchedule equivalent (they cover poisons and prohibited
    /// substances rather than medicines) and are treated as unknown rather than forced into a value.
    /// </summary>
    public static MedicineSchedule? MapSchedule(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (raw.Contains("(S8)", StringComparison.OrdinalIgnoreCase)) return MedicineSchedule.S8;
        if (raw.Contains("(S4)", StringComparison.OrdinalIgnoreCase)) return MedicineSchedule.S4;
        if (raw.Contains("(S3)", StringComparison.OrdinalIgnoreCase)) return MedicineSchedule.S3;
        if (raw.Contains("(S2)", StringComparison.OrdinalIgnoreCase)) return MedicineSchedule.S2;

        // "Not scheduled. Not considered by committee" / "Not Scheduled after consideration by
        // Committee" — a real finding of "no scheduling applies", not missing data.
        if (raw.Contains("not scheduled", StringComparison.OrdinalIgnoreCase))
            return MedicineSchedule.Unscheduled;

        return null;
    }

    private static int IndexOf(List<string> header, string name) =>
        header.FindIndex(f => string.Equals(f.Trim(), name, StringComparison.OrdinalIgnoreCase));

    private static string? At(List<string> fields, int index) =>
        index >= 0 && index < fields.Count ? fields[index] : null;

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>RFC4180-style reader: quoted fields may contain commas, newlines and escaped quotes
    /// (""). Streams rather than materialising the file — the full export runs to ~95 MB.</summary>
    private static IEnumerable<List<string>> ReadRecords(TextReader reader)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var sawAny = false;
        int ch;

        while ((ch = reader.Read()) != -1)
        {
            var c = (char)ch;

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"') { reader.Read(); current.Append('"'); }  // escaped quote
                    else inQuotes = false;
                }
                else current.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    sawAny = true;
                    break;
                case ',':
                    fields.Add(current.ToString());
                    current.Clear();
                    sawAny = true;
                    break;
                case '\r':
                    break;   // handled by the \n that follows
                case '\n':
                    fields.Add(current.ToString());
                    current.Clear();
                    if (sawAny) yield return new List<string>(fields);
                    fields.Clear();
                    sawAny = false;
                    break;
                default:
                    current.Append(c);
                    sawAny = true;
                    break;
            }
        }

        if (sawAny || current.Length > 0)
        {
            fields.Add(current.ToString());
            yield return fields;
        }
    }
}

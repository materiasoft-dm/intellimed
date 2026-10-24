using System.Globalization;
using System.Text;
using IntelliMed.Core.Entities;

namespace IntelliMed.Infrastructure.Hl7;

/// <summary>The fields of an HL7 v2 ORU^R01 result message that IntelliMed uses.</summary>
public class ParsedResult
{
    public string SenderName { get; set; } = string.Empty;
    public string MessageControlId { get; set; } = string.Empty;

    public string? PatientIdentifier { get; set; }
    public string? PatientFamilyName { get; set; }
    public string? PatientGivenName { get; set; }
    public DateTime? PatientDateOfBirth { get; set; }
    public string? PatientSex { get; set; }
    public string? PatientMedicareNumber { get; set; }

    public string? OrderingProviderName { get; set; }
    public string? OrderingProviderNumber { get; set; }

    public string? PlacerOrderNumber { get; set; }
    public string? FillerOrderNumber { get; set; }
    public string? ReportName { get; set; }
    public ResultReportKind Kind { get; set; } = ResultReportKind.Pathology;
    public DateTime? CollectedAt { get; set; }
    public DateTime? ReportedAt { get; set; }

    public List<ResultItem> Items { get; } = new();
    public List<ResultAttachment> Attachments { get; } = new();

    public string RawMessage { get; set; } = string.Empty;
}

public class Hl7ParseException : Exception
{
    public Hl7ParseException(string message) : base(message) { }
}

/// <summary>
/// Reads an HL7 v2 ORU^R01 (unsolicited observation result) message — the format Australian
/// pathology and radiology labs send results in. Deliberately tolerant of the variation between
/// labs (segment terminators, trailing separators, missing optional segments) but strict about the
/// things that make a result unsafe to file: it must be an ORU message and it must carry a patient.
///
/// Field numbering follows the HL7 standard: for every segment except MSH, field N is
/// <c>parts[N]</c>; MSH-1 is the field separator itself so MSH-N is <c>parts[N - 1]</c>.
/// </summary>
public class Hl7ResultParser
{
    public ParsedResult Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new Hl7ParseException("The message is empty.");

        // Some transports wrap the message in MLLP framing (0x0B ... 0x1C 0x0D).
        var text = message.Trim('\u000B', '\u001C', '﻿', ' ', '\r', '\n', '\t');
        if (!text.StartsWith("MSH", StringComparison.Ordinal))
            throw new Hl7ParseException("Not an HL7 v2 message: it does not start with an MSH segment.");

        var segments = text
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.TrimEnd('\u001C'))
            .Where(s => s.Length >= 3)
            .ToList();

        var fieldSep = text.Length > 3 ? text[3] : '|';
        var encoding = segments[0].Split(fieldSep).ElementAtOrDefault(1) ?? "^~\\&";
        var enc = new Encoding7(encoding);

        var result = new ParsedResult { RawMessage = text };
        ResultItem? lastItem = null;
        var reportComments = new List<string>();
        var sawObr = false;
        var sawPid = false;

        foreach (var segment in segments)
        {
            var parts = segment.Split(fieldSep);
            switch (parts[0])
            {
                case "MSH":
                    ReadMsh(parts, enc, result);
                    break;

                case "PID":
                    sawPid = true;
                    ReadPid(parts, enc, result);
                    break;

                case "PV1":
                    // PV1-8 referring doctor — a fallback for labs that leave OBR-16 empty.
                    if (result.OrderingProviderName == null)
                        ReadProvider(Field(parts, 8), enc, result);
                    break;

                case "ORC":
                    // ORC-12 ordering provider.
                    if (result.OrderingProviderName == null)
                        ReadProvider(Field(parts, 12), enc, result);
                    result.PlacerOrderNumber ??= NullIfBlank(enc.Component(Field(parts, 2), 1));
                    result.FillerOrderNumber ??= NullIfBlank(enc.Component(Field(parts, 3), 1));
                    break;

                case "OBR":
                    ReadObr(parts, enc, result, ref sawObr);
                    lastItem = null;
                    break;

                case "OBX":
                    lastItem = ReadObx(parts, enc, result);
                    break;

                case "NTE":
                    var note = Unescape(enc, Field(parts, 3));
                    if (!string.IsNullOrWhiteSpace(note))
                    {
                        if (lastItem != null)
                            lastItem.Comment = string.IsNullOrEmpty(lastItem.Comment) ? note : lastItem.Comment + "\n" + note;
                        else
                            reportComments.Add(note);
                    }
                    break;
            }
        }

        if (!sawPid)
            throw new Hl7ParseException("The message has no PID segment, so it cannot be tied to a patient.");

        if (string.IsNullOrWhiteSpace(result.MessageControlId))
            throw new Hl7ParseException("The message has no control id (MSH-10).");

        // Report-level comments (NTE before any OBX) are kept as an item so they are not lost.
        if (reportComments.Count > 0)
        {
            result.Items.Insert(0, new ResultItem
            {
                Name = "Comment",
                Value = string.Join("\n", reportComments),
                ResultStatus = null
            });
        }

        for (var i = 0; i < result.Items.Count; i++)
            result.Items[i].Sequence = i + 1;

        return result;
    }

    // ---- segments ----

    private static void ReadMsh(string[] parts, Encoding7 enc, ParsedResult r)
    {
        // MSH-N is parts[N - 1].
        var sendingApp = enc.Component(MshField(parts, 3), 1);
        var sendingFacility = enc.Component(MshField(parts, 4), 1);
        r.SenderName = !string.IsNullOrWhiteSpace(sendingFacility) ? sendingFacility.Trim() : (sendingApp ?? string.Empty).Trim();

        var messageType = MshField(parts, 9);
        var type = enc.Component(messageType, 1);
        if (!string.Equals(type, "ORU", StringComparison.OrdinalIgnoreCase))
            throw new Hl7ParseException($"Unsupported message type '{messageType}'. Only ORU (result) messages are accepted.");

        r.MessageControlId = (MshField(parts, 10) ?? string.Empty).Trim();
    }

    private static void ReadPid(string[] parts, Encoding7 enc, ParsedResult r)
    {
        // PID-3 is a repeating list: id^check^scheme^authority^typecode. Medicare is typed MC / AUSHIC.
        foreach (var rep in enc.Repetitions(Field(parts, 3)))
        {
            var id = NullIfBlank(enc.Component(rep, 1));
            if (id == null) continue;
            var authority = enc.Component(rep, 4) ?? string.Empty;
            var typeCode = enc.Component(rep, 5) ?? string.Empty;

            var isMedicare = typeCode.Equals("MC", StringComparison.OrdinalIgnoreCase)
                || authority.Contains("AUSHIC", StringComparison.OrdinalIgnoreCase)
                || authority.Contains("Medicare", StringComparison.OrdinalIgnoreCase);

            if (isMedicare) r.PatientMedicareNumber ??= DigitsOnly(id);
            else r.PatientIdentifier ??= id;
        }

        // Older feeds put the lab MRN in PID-2.
        r.PatientIdentifier ??= NullIfBlank(enc.Component(Field(parts, 2), 1));

        var name = enc.Repetitions(Field(parts, 5)).FirstOrDefault();
        if (name != null)
        {
            r.PatientFamilyName = NullIfBlank(Unescape(enc, enc.Component(name, 1)));
            r.PatientGivenName = NullIfBlank(Unescape(enc, enc.Component(name, 2)));
        }

        r.PatientDateOfBirth = ParseTimestamp(Field(parts, 7));
        r.PatientSex = NullIfBlank(Field(parts, 8));
    }

    private static void ReadObr(string[] parts, Encoding7 enc, ParsedResult r, ref bool sawObr)
    {
        // Several OBRs in one message are folded into one report: the first supplies the header.
        if (!sawObr)
        {
            sawObr = true;
            r.PlacerOrderNumber = NullIfBlank(enc.Component(Field(parts, 2), 1)) ?? r.PlacerOrderNumber;
            r.FillerOrderNumber = NullIfBlank(enc.Component(Field(parts, 3), 1)) ?? r.FillerOrderNumber;

            var service = Field(parts, 4);
            r.ReportName = NullIfBlank(Unescape(enc, enc.Component(service, 2)))
                           ?? NullIfBlank(Unescape(enc, enc.Component(service, 1)));

            r.CollectedAt = ParseTimestamp(Field(parts, 7));
            r.ReportedAt = ParseTimestamp(Field(parts, 22));

            // OBR-24 diagnostic service section: RAD / CT / MR / US / NM etc. mean imaging.
            var section = (Field(parts, 24) ?? string.Empty).Trim().ToUpperInvariant();
            if (section is "RAD" or "RX" or "CT" or "MR" or "MRI" or "US" or "NM" or "XR" or "IMG")
                r.Kind = ResultReportKind.Imaging;

            if (r.OrderingProviderName == null)
                ReadProvider(Field(parts, 16), enc, r);
        }
    }

    private static ResultItem? ReadObx(string[] parts, Encoding7 enc, ParsedResult r)
    {
        var valueType = (Field(parts, 2) ?? string.Empty).Trim().ToUpperInvariant();
        var identifier = Field(parts, 3);
        var code = NullIfBlank(enc.Component(identifier, 1));
        var name = NullIfBlank(Unescape(enc, enc.Component(identifier, 2))) ?? code ?? "Result";
        var value = Field(parts, 5);

        // Embedded document (ED) or reference pointer (RP): source^type^subtype^encoding^data.
        if (valueType == "ED")
        {
            var subtype = enc.Component(value, 3) ?? "pdf";
            var codec = enc.Component(value, 4) ?? string.Empty;
            var data = enc.Component(value, 5) ?? string.Empty;
            if (codec.Equals("Base64", StringComparison.OrdinalIgnoreCase) && data.Length > 0)
            {
                try
                {
                    var bytes = Convert.FromBase64String(data);
                    var ext = subtype.ToLowerInvariant();
                    r.Attachments.Add(new ResultAttachment
                    {
                        FileName = $"{SafeFileName(name)}.{ext}",
                        ContentType = ContentTypeFor(ext),
                        Data = bytes
                    });
                }
                catch (FormatException)
                {
                    // A corrupt attachment must not lose the structured results around it.
                    r.Items.Add(new ResultItem { Code = code, Name = name, Value = "[attachment could not be read]" });
                }
            }
            return null;
        }

        var item = new ResultItem
        {
            Code = code,
            Name = name,
            Value = NullIfBlank(valueType is "CE" or "CWE" or "CNE"
                ? Unescape(enc, enc.Component(value, 2) ?? enc.Component(value, 1))
                : Unescape(enc, value)),
            Units = NullIfBlank(Unescape(enc, enc.Component(Field(parts, 6), 1))),
            ReferenceRange = NullIfBlank(Unescape(enc, Field(parts, 7))),
            AbnormalFlag = NullIfBlank(Field(parts, 8)),
            ResultStatus = NullIfBlank(Field(parts, 11))
        };

        // HL7: empty or N = normal. Anything else (H, L, HH, LL, A, AA, <, >, U, W...) is flagged.
        item.IsAbnormal = item.AbnormalFlag != null
            && !item.AbnormalFlag.Equals("N", StringComparison.OrdinalIgnoreCase);

        r.Items.Add(item);
        return item;
    }

    private static void ReadProvider(string? field, Encoding7 enc, ParsedResult r)
    {
        var rep = enc.Repetitions(field).FirstOrDefault();
        if (rep == null) return;

        // XCN: id^family^given^middle^suffix^prefix
        var number = NullIfBlank(enc.Component(rep, 1));
        var family = NullIfBlank(Unescape(enc, enc.Component(rep, 2)));
        var given = NullIfBlank(Unescape(enc, enc.Component(rep, 3)));
        var prefix = NullIfBlank(Unescape(enc, enc.Component(rep, 6)));

        if (number == null && family == null) return;

        r.OrderingProviderNumber = number;
        r.OrderingProviderName = string.Join(" ", new[] { prefix, given, family }.Where(s => s != null));
    }

    // ---- helpers ----

    private static string? Field(string[] parts, int number) =>
        number < parts.Length ? parts[number] : null;

    private static string? MshField(string[] parts, int number) =>
        number - 1 < parts.Length ? parts[number - 1] : null;

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string DigitsOnly(string s) => new(s.Where(char.IsDigit).ToArray());

    private static string SafeFileName(string name)
    {
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').ToArray()).Trim();
        return cleaned.Length == 0 ? "report" : cleaned.Replace(' ', '_');
    }

    private static string ContentTypeFor(string ext) => ext switch
    {
        "pdf" => "application/pdf",
        "png" => "image/png",
        "jpg" or "jpeg" => "image/jpeg",
        "rtf" => "application/rtf",
        "txt" => "text/plain",
        _ => "application/octet-stream"
    };

    /// <summary>HL7 timestamps: yyyy[MM[dd[HH[mm[ss[.S+]]]]]][+/-ZZZZ]. Returns null when unparseable.</summary>
    public static DateTime? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var digits = new StringBuilder();
        foreach (var c in value.Trim())
        {
            if (char.IsDigit(c)) digits.Append(c);
            else break; // stop at ".", "+", "-" (fraction / zone)
        }

        var s = digits.ToString();
        string format = s.Length switch
        {
            >= 14 => "yyyyMMddHHmmss",
            12 => "yyyyMMddHHmm",
            10 => "yyyyMMddHH",
            8 => "yyyyMMdd",
            6 => "yyyyMM",
            4 => "yyyy",
            _ => string.Empty
        };
        if (format.Length == 0) return null;

        s = s[..format.Length];
        return DateTime.TryParseExact(s, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? dt
            : null;
    }

    /// <summary>Replaces HL7 escape sequences (\F\ \S\ \R\ \T\ \E\ and \.br\) with their characters.</summary>
    private static string? Unescape(Encoding7 enc, string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains(enc.Escape)) return value;

        var sb = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != enc.Escape) { sb.Append(value[i]); continue; }

            var end = value.IndexOf(enc.Escape, i + 1);
            if (end < 0) { sb.Append(value[i]); continue; }

            var code = value.Substring(i + 1, end - i - 1);
            switch (code)
            {
                case "F": sb.Append(enc.FieldSeparator); break;
                case "S": sb.Append(enc.ComponentSeparator); break;
                case "R": sb.Append(enc.RepetitionSeparator); break;
                case "T": sb.Append(enc.SubcomponentSeparator); break;
                case "E": sb.Append(enc.Escape); break;
                case ".br": sb.Append('\n'); break;
                default:
                    // Unknown sequences (highlighting \H\ \N\, charset switches) carry no text — drop them.
                    break;
            }
            i = end;
        }
        return sb.ToString();
    }

    /// <summary>The encoding characters declared in MSH-2 (normally ^~\&amp;).</summary>
    private sealed class Encoding7
    {
        public char FieldSeparator { get; } = '|';
        public char ComponentSeparator { get; }
        public char RepetitionSeparator { get; }
        public char Escape { get; }
        public char SubcomponentSeparator { get; }

        public Encoding7(string declared)
        {
            ComponentSeparator = declared.Length > 0 ? declared[0] : '^';
            RepetitionSeparator = declared.Length > 1 ? declared[1] : '~';
            Escape = declared.Length > 2 ? declared[2] : '\\';
            SubcomponentSeparator = declared.Length > 3 ? declared[3] : '&';
        }

        public IEnumerable<string> Repetitions(string? field) =>
            string.IsNullOrEmpty(field)
                ? Enumerable.Empty<string>()
                : field.Split(RepetitionSeparator).Where(s => s.Length > 0);

        /// <summary>1-based component of a field; null when absent.</summary>
        public string? Component(string? field, int index)
        {
            if (string.IsNullOrEmpty(field)) return null;
            var comps = field.Split(ComponentSeparator);
            return index - 1 < comps.Length ? comps[index - 1] : null;
        }
    }
}

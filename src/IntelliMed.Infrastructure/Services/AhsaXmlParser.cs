using System.Globalization;
using System.Xml.Linq;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Parses AHSA's Access Gap Cover fee schedule XML feed (the format published per-state at
/// doctors.ahsa.com.au, e.g. "NSW AGC fee schedule - XML"): a flat list of &lt;MBS&gt; records,
/// each carrying either a fixed-dollar benefit or a percentage-of-MBS-fee benefit (never both).
/// </summary>
public static class AhsaXmlParser
{
    public const string RootElementName = "AccessGapCover";

    public readonly record struct AhsaFeeRecord(string ItemNumber, decimal? DollarBenefit, decimal? PercentageBenefit);

    /// <summary>True if the downloaded text looks like an AHSA AccessGapCover XML document.</summary>
    public static bool TryParse(string xmlText, out List<AhsaFeeRecord> records)
    {
        records = [];
        XDocument document;
        try
        {
            document = XDocument.Parse(xmlText);
        }
        catch
        {
            return false;
        }

        if (document.Root?.Name.LocalName != RootElementName)
            return false;

        // The feed declares a default xmlns (e.g. "http://www.ahsa.com.au/schema/agc"), which applies
        // to every unprefixed element — so lookups must match by local name, not exact XName, or
        // Elements("MBS") silently returns nothing. Descendants (not Elements) because &lt;MBS&gt;
        // records sit inside a wrapping container, not directly under the root.
        foreach (var mbs in document.Root.Descendants().Where(e => e.Name.LocalName == "MBS"))
        {
            var itemNumber = NormalizeItemNumber(ChildValue(mbs, "MBSCode"));
            if (string.IsNullOrEmpty(itemNumber)) continue;

            var dollarBenefit = ParseNullableDecimal(ChildValue(mbs, "AHSADollarBenefit"));
            var percentageBenefit = ParseNullableDecimal(ChildValue(mbs, "AHSAPercentageBenefit"));
            if (dollarBenefit == null && percentageBenefit == null) continue;

            records.Add(new AhsaFeeRecord(itemNumber, dollarBenefit, percentageBenefit));
        }

        return true;
    }

    private static string? ChildValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    /// <summary>AHSA zero-pads MBS item codes (e.g. "00104"); our BillingItem catalog does not.</summary>
    private static string NormalizeItemNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var trimmed = raw.Trim().TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    private static decimal? ParseNullableDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : null;
}

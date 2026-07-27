namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Parses a derived-item association list (comma/space-separated MBS item numbers, where an entry
/// containing "-" expands to every integer in that range) into a matchable set. Ported from legacy's
/// MbsDerivedItemInfo.BuildListItemNumFromString — this is the syntax real MBS DerivedFee text needs,
/// e.g. item 25030's "item range 25200-25205, plus item range 23010-24136...".
/// </summary>
public static class ItemNumberRangeMatcher
{
    private static readonly char[] SplitChars = { ',', ' ' };

    public static HashSet<string> Parse(string? input)
    {
        var result = new HashSet<string>();
        if (string.IsNullOrEmpty(input)) return result;

        foreach (var token in input.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!token.Contains('-'))
            {
                result.Add(token);
                continue;
            }

            var parts = token.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var from) && int.TryParse(parts[1], out var to))
            {
                if (from > to) (from, to) = (to, from);
                for (var i = from; i <= to; i++)
                    result.Add(i.ToString());
            }
            else
            {
                foreach (var part in parts)
                    result.Add(part);
            }
        }

        return result;
    }
}

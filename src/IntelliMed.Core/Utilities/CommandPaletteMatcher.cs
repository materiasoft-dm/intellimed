using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Utilities;

/// <summary>
/// Matches command-palette search queries against actions. Tokenizes the query on whitespace and
/// requires every token to appear somewhere in the action's Title/Keywords/Description — order
/// independent, so "create client" and "create patient" both match an action titled "Add Client"
/// whose Keywords include "create" and "patient".
/// </summary>
public static class CommandPaletteMatcher
{
    public static bool Matches(SearchActionDto action, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;

        var haystack = $"{action.Title} {action.Keywords} {action.Description}".ToLowerInvariant();
        var tokens = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return tokens.Length > 0 && tokens.All(token => haystack.Contains(token));
    }

    public static List<SearchActionDto> Filter(IEnumerable<SearchActionDto> actions, string query) =>
        actions.Where(a => Matches(a, query)).ToList();
}

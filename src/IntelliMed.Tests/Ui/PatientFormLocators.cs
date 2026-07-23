using Microsoft.Playwright;

namespace IntelliMed.Tests.Ui;

/// <summary>
/// Locator helpers for AddPatient.razor. Most text/select/date fields on that page use a plain
/// &lt;label&gt; immediately followed by the input, without a for/id association, so they can't be
/// found via Playwright's GetByLabel(). These helpers locate the input as the label's next
/// sibling instead. Checkboxes on that page DO have proper for/id pairs, so use
/// Page.GetByLabel(...) for those directly.
/// </summary>
public static class PatientFormLocators
{
    /// <summary>Finds the input/select/textarea immediately following a &lt;label&gt; with the given exact text, anywhere on the page.</summary>
    public static ILocator Field(this IPage page, string label) =>
        page.Locator(
            $"xpath=//label[normalize-space(text())={XPathLiteral(label)}]/following-sibling::*[self::input or self::select or self::textarea][1]");

    /// <summary>Finds the input/select/textarea immediately following a &lt;label&gt; with the given exact text, scoped to the card whose header contains cardHeader. Use this for labels that repeat across multiple cards (e.g. "EXP", "Ref").</summary>
    public static ILocator FieldInCard(this IPage page, string cardHeader, string label)
    {
        var card = page.Locator(".card").Filter(new LocatorFilterOptions { HasText = cardHeader }).First;
        return card.Locator(
            $"xpath=.//label[normalize-space(text())={XPathLiteral(label)}]/following-sibling::*[self::input or self::select or self::textarea][1]");
    }

    /// <summary>Finds the nav-tab link with the given exact text within the bottom tab strip or address tab strip.</summary>
    public static ILocator TabLink(this IPage page, string tabText) =>
        page.Locator("a.nav-link", new PageLocatorOptions { HasTextString = tabText });

    // XPath 1.0 has no escaping for quotes inside string literals, so build a concat() expression
    // when the label itself contains a single quote (none currently do, but this keeps it safe).
    private static string XPathLiteral(string value) =>
        value.Contains('\'') ? $"concat('{value.Replace("'", "', \"'\", '")}')" : $"'{value}'";
}

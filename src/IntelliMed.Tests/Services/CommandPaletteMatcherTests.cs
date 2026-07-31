using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Utilities;
using Xunit;

namespace IntelliMed.Tests.Services;

public class CommandPaletteMatcherTests
{
    private static SearchActionDto AddClientAction() => new()
    {
        Title = "Add Client",
        Keywords = "create new patient register",
        Description = "Create a new client record"
    };

    private static SearchActionDto NewInvoiceAction() => new()
    {
        Title = "New Invoice",
        Keywords = "create new bill",
        Description = "Create a new invoice"
    };

    [Fact]
    public void Matches_EmptyQuery_MatchesEverything()
    {
        CommandPaletteMatcher.Matches(AddClientAction(), "").Should().BeTrue();
    }

    [Fact]
    public void Matches_KeywordSynonym_MatchesEvenWhenNotInTitle()
    {
        // "patient" never appears in the Title ("Add Client") — only in Keywords.
        CommandPaletteMatcher.Matches(AddClientAction(), "patient").Should().BeTrue();
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        CommandPaletteMatcher.Matches(AddClientAction(), "PATIENT").Should().BeTrue();
    }

    [Fact]
    public void Matches_MultiWordQuery_RequiresEveryTokenPresent()
    {
        // "create client" and "create patient" both work — order-independent, both tokens present.
        CommandPaletteMatcher.Matches(AddClientAction(), "create client").Should().BeTrue();
        CommandPaletteMatcher.Matches(AddClientAction(), "create patient").Should().BeTrue();
    }

    [Fact]
    public void Matches_MultiWordQuery_FailsWhenOneTokenMissing()
    {
        CommandPaletteMatcher.Matches(AddClientAction(), "create invoice").Should().BeFalse();
    }

    [Fact]
    public void Matches_NoMatch_ReturnsFalse()
    {
        CommandPaletteMatcher.Matches(AddClientAction(), "xyzzy").Should().BeFalse();
    }

    [Fact]
    public void Filter_CreateQuery_ReturnsAllActionsWithCreateKeyword()
    {
        var actions = new[] { AddClientAction(), NewInvoiceAction() };

        var result = CommandPaletteMatcher.Filter(actions, "create");

        result.Should().HaveCount(2);
    }
}

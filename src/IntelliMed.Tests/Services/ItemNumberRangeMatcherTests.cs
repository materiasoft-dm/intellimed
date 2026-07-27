using FluentAssertions;
using IntelliMed.Infrastructure.Services;
using Xunit;

namespace IntelliMed.Tests.Services;

public class ItemNumberRangeMatcherTests
{
    [Fact]
    public void Parse_Null_ReturnsEmptySet()
    {
        ItemNumberRangeMatcher.Parse(null).Should().BeEmpty();
    }

    [Fact]
    public void Parse_CommaSeparatedList_ReturnsEachItem()
    {
        var result = ItemNumberRangeMatcher.Parse("51300,51303,23010");

        result.Should().BeEquivalentTo(new[] { "51300", "51303", "23010" });
    }

    [Fact]
    public void Parse_SpaceSeparatedList_ReturnsEachItem()
    {
        var result = ItemNumberRangeMatcher.Parse("51300 51303 23010");

        result.Should().BeEquivalentTo(new[] { "51300", "51303", "23010" });
    }

    [Fact]
    public void Parse_Range_ExpandsToEveryIntegerInBetween()
    {
        var result = ItemNumberRangeMatcher.Parse("25200-25205");

        result.Should().BeEquivalentTo(new[] { "25200", "25201", "25202", "25203", "25204", "25205" });
    }

    [Fact]
    public void Parse_ReversedRange_StillExpandsCorrectly()
    {
        var result = ItemNumberRangeMatcher.Parse("25205-25200");

        result.Should().BeEquivalentTo(new[] { "25200", "25201", "25202", "25203", "25204", "25205" });
    }

    [Fact]
    public void Parse_MixedListAndRanges_ExpandsRangesAndKeepsSingles()
    {
        // Mirrors real MBS DerivedFee text, e.g. item 25030's associated-item description.
        var result = ItemNumberRangeMatcher.Parse("25200-25205, 23010-24136, 22002-22051");

        result.Should().Contain("25200").And.Contain("25205").And.Contain("23500").And.Contain("22030");
        result.Count.Should().Be(6 + (24136 - 23010 + 1) + (22051 - 22002 + 1));
    }
}

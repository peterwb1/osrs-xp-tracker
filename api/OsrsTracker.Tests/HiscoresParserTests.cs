using FluentAssertions;
using OsrsTracker.Domain.Hiscores;

namespace OsrsTracker.Tests;

public class HiscoresParserTests
{
    private const string SampleCsv =
        "123,2277,200000000\n" + // Overall
        "456,99,13034431\n" +    // Attack
        "789,99,13034431\n" +    // Defence
        "-1,-1,-1\n";            // Unranked skill

    [Fact]
    public void Parse_ReturnsCorrectNumberOfEntries()
    {
        var result = HiscoresParser.Parse(SampleCsv);
        result.Should().HaveCount(4);
    }

    [Fact]
    public void Parse_ReadsOverallCorrectly()
    {
        var result = HiscoresParser.Parse(SampleCsv);
        result[0].Rank.Should().Be(123);
        result[0].Level.Should().Be(2277);
        result[0].Xp.Should().Be(200_000_000);
    }

    [Fact]
    public void Parse_HandlesUnrankedSkill()
    {
        var result = HiscoresParser.Parse(SampleCsv);
        result[3].Rank.Should().Be(-1);
        result[3].Level.Should().Be(-1);
        result[3].Xp.Should().Be(-1);
    }

    [Fact]
    public void Parse_IgnoresEmptyLines()
    {
        var csv = "123,2277,200000000\n\n\n456,99,13034431\n";
        var result = HiscoresParser.Parse(csv);
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_ReturnsEmptyList_WhenInputIsEmpty()
    {
        var result = HiscoresParser.Parse(string.Empty);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SkipsInvalidLines()
    {
        var csv = "not,valid,data\n123,99,1000\n";
        var result = HiscoresParser.Parse(csv);
        result.Should().HaveCount(1);
        result[0].Xp.Should().Be(1000);
    }
}

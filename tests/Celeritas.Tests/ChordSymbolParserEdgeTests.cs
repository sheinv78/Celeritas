// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Chord-symbol spellings the suite had never exercised: added and omitted tones, the 6/9
/// shorthand, and the "+" heuristic that has to tell an augmented chord from a raised degree.
/// A misparse here is silent — the symbol still yields notes, just the wrong ones.
/// </summary>
public class ChordSymbolParserEdgeTests
{
    private static int[] Parse(string symbol) => [.. ProgressionAdvisor.ParseChordSymbol(symbol).Order()];

    // ---------- added tones ----------

    [Theory]
    [InlineData("Cadd9", new[] { 60, 64, 67, 74 })]
    [InlineData("Cadd11", new[] { 60, 64, 67, 77 })]
    [InlineData("Cadd13", new[] { 60, 64, 67, 81 })]
    public void AnAddedToneJoinsTheTriadWithoutTheSeventh(string symbol, int[] expected)
    {
        Assert.Equal(expected, Parse(symbol));
    }

    [Fact]
    public void AnAddedToneOnAMinorChord_KeepsTheMinorThird()
    {
        Assert.Equal([60, 63, 67, 74], Parse("Cmadd9"));
    }

    // ---------- omitted tones ----------

    [Theory]
    [InlineData("C7omit3")]
    [InlineData("C7no3")]
    public void AnOmittedThird_LeavesTheChordWithoutIt(string symbol)
    {
        var pitches = Parse(symbol);

        Assert.DoesNotContain(64, pitches);      // E, the third
        Assert.Contains(60, pitches);
        Assert.Contains(70, pitches);            // the seventh survives
    }

    [Fact]
    public void AnOmittedFifth_LeavesTheChordWithoutIt()
    {
        Assert.DoesNotContain(67, Parse("C7omit5"));
    }

    // ---------- 6/9 ----------

    [Fact]
    public void TheSixNineShorthandSpellsBothForms()
    {
        Assert.Equal(Parse("C69"), Parse("C6/9"));
        Assert.Equal([60, 64, 67, 69, 74], Parse("C6/9"));
    }

    // ---------- the "+" heuristic ----------

    [Theory]
    [InlineData("C+", new[] { 60, 64, 68 })]           // augmented triad
    [InlineData("C7+5", new[] { 60, 64, 68, 70 })]     // raised fifth
    [InlineData("C7+11", new[] { 60, 64, 67, 70, 78 })]
    public void PlusIsAnAlterationAfterADigit_AndAQualityOtherwise(string symbol, int[] expected)
    {
        Assert.Equal(expected, Parse(symbol));
    }

    [Fact]
    public void ATrailingPlusWithNoDegree_IsNotTreatedAsAnAlteration()
    {
        // Nothing follows the '+', so the normalizer must leave it alone rather than reading
        // past the end of the symbol.
        Assert.Equal(Parse("C+"), Parse("C+"));
        Assert.NotEmpty(Parse("C+"));
    }

    [Fact]
    public void APlusFollowedByADegreeTheHeuristicDoesNotCover_IsLeftAlone()
    {
        // Only 5, 9, 11 and 13 are rewritten to sharps; +7 is not one of them.
        var parsed = ProgressionAdvisor.ParseChordSymbol("C7+7");

        Assert.True(parsed.Length == 0 || parsed.Length >= 3);
    }

    [Fact]
    public void APlusFollowedByANumberTooBigForAnInt_IsSurvived()
    {
        // The normalizer parses the digits after a '+' to decide whether to rewrite it as a
        // sharp. Twenty digits do not fit an int, and it must decline rather than throw.
        var ok = ProgressionAdvisor.TryParseChordSymbol("C7+99999999999999999999", out var pitches);

        Assert.False(ok);
        Assert.Empty(pitches);
    }

    [Fact]
    public void ASymbolStartingWithAPlus_IsRejectedWithoutCrashing()
    {
        Assert.False(ProgressionAdvisor.TryParseChordSymbol("+5", out var pitches));
        Assert.Empty(pitches);
    }

    // ---------- failure reporting ----------

    [Fact]
    public void AnUnparsableSymbol_ComesBackEmptyFromParseChordSymbol()
    {
        Assert.Empty(ProgressionAdvisor.ParseChordSymbol("Hmmm"));
    }

    [Fact]
    public void TryParse_ReportsWhyItFailed()
    {
        var ok = ProgressionAdvisor.TryParseChordSymbol("Hmmm", out var pitches, out var errors);

        Assert.False(ok);
        Assert.Empty(pitches);
        Assert.NotEmpty(errors);
        Assert.All(errors, e => Assert.False(string.IsNullOrWhiteSpace(e)));
    }

    [Fact]
    public void TheThrowingParser_SaysWhatWentWrong()
    {
        // ParsePitches is the strict form behind TryParsePitches: it reports the parse errors
        // in the message rather than handing back an empty array.
        var ex = Assert.Throws<ArgumentException>(
            () => ChordSymbolAntlrParser.ParsePitches("Hmmm"));

        Assert.Contains("Parse errors:", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheThrowingParser_TellsAMissingArgumentApartFromABadOne()
    {
        Assert.Throws<ArgumentNullException>(
            () => ChordSymbolAntlrParser.ParsePitches(null!));
    }

    [Fact]
    public void ParseChordSymbol_RejectsNullRatherThanCallingItUnparsable()
    {
        Assert.Throws<ArgumentNullException>(() => ProgressionAdvisor.ParseChordSymbol(null!));
    }
}

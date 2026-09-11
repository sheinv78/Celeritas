// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// A bare number after a root is lead-sheet shorthand: "C2" is the C add9 of a Nashville chart,
/// "C4" its C sus4, "C5" the power chord. The parser already read "C5" that way, but "C2" and
/// "C4" came back as a plain C major triad — the number was accepted and then ignored, because
/// the builder only acted on 6 and on 7 and above — and "C3", "C8", "C10" and "C15", which mean
/// nothing at all, parsed too: C3 to the same triad, C8 to a C7, C10 to a C9. The changelog's
/// policy for this parser is that a degree it cannot
/// give a meaning to fails the parse rather than being dropped; a bare extension was the one
/// place that policy had not reached.
/// </summary>
public class ABareNumberMeansWhatTheLeadSheetMeansTests
{
    private static readonly string[] Roots =
        ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    private static int[] Parse(string symbol) => [.. ProgressionAdvisor.ParseChordSymbol(symbol).Order()];

    [Fact]
    public void ABareTwoIsTheAddedNinth()
    {
        Assert.Equal([60, 64, 67, 74], Parse("C2"));

        foreach (var root in Roots)
            Assert.Equal(Parse(root + "add9"), Parse(root + "2"));
    }

    [Fact]
    public void ABareFourIsTheSuspendedFourth()
    {
        Assert.Equal([60, 65, 67], Parse("C4"));

        foreach (var root in Roots)
            Assert.Equal(Parse(root + "sus4"), Parse(root + "4"));
    }

    [Fact]
    public void ABareFiveIsStillThePowerChord()
    {
        Assert.Equal([60, 67], Parse("C5"));
    }

    [Theory]
    [InlineData("C2", ChordQuality.Add9)]
    [InlineData("C4", ChordQuality.Sus4)]
    [InlineData("C5", ChordQuality.Power)]
    public void TheAnalyzerReadsTheShorthandAsTheChordItNames(string symbol, ChordQuality expected)
    {
        var chord = ChordAnalyzer.Identify(ProgressionAdvisor.ParseChordSymbol(symbol));

        Assert.Equal(new ChordInfo(0, expected), chord);
    }

    [Fact]
    public void TheShorthandInsideParenthesesIsTheSameAsOutside()
    {
        Assert.Equal(Parse("C2"), Parse("C(2)"));
        Assert.Equal(Parse("C4"), Parse("C(4)"));
        Assert.Equal(Parse("C5"), Parse("C(5)"));
    }

    [Fact]
    public void ASuspensionInsideParenthesesIsTheSameAsOutside()
    {
        // The parenthesized path had no "sus then 2" rule of its own, so "C(sus2)" was read as
        // a sus4 — the number was accepted as an extension and then ignored.
        Assert.Equal([60, 62, 67], Parse("C(sus2)"));
        Assert.Equal(Parse("Csus2"), Parse("C(sus2)"));
        Assert.Equal(Parse("Csus4"), Parse("C(sus4)"));
    }

    [Fact]
    public void TheShorthandKeepsTheQualityWrittenBeforeIt()
    {
        // "Am2" is the minor add9, the way "Amadd9" is; the 2 adds a ninth, it does not
        // overwrite the third.
        Assert.Equal(Parse("Amadd9"), Parse("Am2"));
        Assert.Equal([69, 72, 76, 83], Parse("Am2"));      // A C E B: the third stays minor
    }

    [Theory]
    [InlineData("C1", 1)]
    [InlineData("C3", 3)]
    [InlineData("C8", 8)]
    [InlineData("C10", 10)]
    [InlineData("C12", 12)]
    [InlineData("C14", 14)]
    [InlineData("C15", 15)]
    [InlineData("Cm3", 3)]
    [InlineData("C(8)", 8)]
    public void ANumberThatIsNotAChordFailsTheParseAndSaysWhich(string symbol, int degree)
    {
        var ok = ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches, out var errors);

        Assert.False(ok);
        Assert.Empty(pitches);
        Assert.Empty(ProgressionAdvisor.ParseChordSymbol(symbol));

        var error = Assert.Single(errors);
        Assert.Contains($"extension: {degree} ", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("C6")]
    [InlineData("C7")]
    [InlineData("C9")]
    [InlineData("C11")]
    [InlineData("C13")]
    [InlineData("Csus2")]
    [InlineData("Csus4")]
    [InlineData("C7sus4")]
    [InlineData("C6/9")]
    public void EveryNumberTheParserAlreadyGaveAMeaningStillParses(string symbol)
    {
        Assert.True(ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches, out var errors));
        Assert.Empty(errors);
        Assert.True(pitches.Length >= 3, symbol);
    }
}

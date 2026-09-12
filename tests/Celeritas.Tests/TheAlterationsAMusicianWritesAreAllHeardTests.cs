// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Chord symbols the tests in <see cref="ADegreeKeepsEveryAlterationWrittenForItTests"/> do not name,
/// each spelled the way a musician spells it. Two alterations of one degree must both be heard
/// whether they are written in one group, in two groups, unparenthesized, with Unicode or "+"
/// accidentals, on any root, under a sus, minor, major-seventh, diminished or augmented chord,
/// over a slash bass or inside a polychord, next to an add or an omit, or beside the "alt"
/// shorthand. The builder used to keep one alteration per degree — the last written — so every
/// row here that names two alterations of one degree lost one of them.
/// </summary>
/// <remarks>
/// The reviewer's held-out symbols for that fix, folded into the suite: 71 checks the fix's
/// author never saw, of which 55 failed before it.
/// </remarks>
public class TheAlterationsAMusicianWritesAreAllHeardTests
{
    private static int[] Parse(string symbol) => [.. ProgressionAdvisor.ParseChordSymbol(symbol).Order()];

    [Theory]
    // unparenthesized chains on other roots
    [InlineData("G7b9#9", new[] { 67, 71, 74, 77, 80, 82 })]
    [InlineData("G7#9b9", new[] { 67, 71, 74, 77, 80, 82 })]
    [InlineData("E7b5#5", new[] { 64, 68, 70, 72, 74 })]
    [InlineData("A7#5b5", new[] { 69, 73, 75, 77, 79 })]
    [InlineData("D7b9#9#11b13", new[] { 62, 66, 69, 72, 75, 77, 80, 82 })]
    // mixed degrees, other roots, the 13 extension
    [InlineData("G7(b5,#9)", new[] { 67, 71, 73, 77, 82 })]
    [InlineData("D13(b9,#9)", new[] { 62, 66, 69, 72, 75, 77, 79, 83 })]
    [InlineData("D13(#9,b9)", new[] { 62, 66, 69, 72, 75, 77, 79, 83 })]
    [InlineData("C13(b9,#9,#11)", new[] { 60, 64, 67, 70, 73, 75, 78, 81 })]
    [InlineData("C13(b9,#9,#11,b13)", new[] { 60, 64, 67, 70, 73, 75, 78, 80 })]
    [InlineData("C7(b5,#5,b9,#9,#11,b13)", new[] { 60, 64, 66, 68, 70, 73, 75, 78, 80 })]
    // with adds and omits
    [InlineData("C7(b9,#9)omit5", new[] { 60, 64, 70, 73, 75 })]
    [InlineData("C7(b9,#9)omit7", new[] { 60, 64, 67, 73, 75 })]
    [InlineData("C7(b5,#5)no3", new[] { 60, 66, 68, 70 })]
    [InlineData("C7(b5,#5)omit5", new[] { 60, 64, 70 })]
    [InlineData("C7(b9,#9)add13", new[] { 60, 64, 67, 70, 73, 75, 81 })]
    [InlineData("C7(b5,#5)add9", new[] { 60, 64, 66, 68, 70, 74 })]
    [InlineData("Cmaj7(b9,#9)omit5", new[] { 60, 64, 71, 73, 75 })]
    // slash bass (octave 3, its own pitch class not doubled) and polychords (second layer an octave up)
    [InlineData("C7(b9,#9)/E", new[] { 52, 60, 67, 70, 73, 75 })]
    [InlineData("G7(b9,#9)/B", new[] { 59, 67, 74, 77, 80, 82 })]
    [InlineData("C7(b5,#5)/Gb", new[] { 54, 60, 64, 68, 70 })]
    [InlineData("F7(b9,#9)/A", new[] { 57, 65, 72, 75, 78, 80 })]
    [InlineData("C|Db7(b9,#9)", new[] { 60, 64, 67, 73, 77, 80, 83, 86, 88 })]
    // "alt" (#5 and b9) beside an explicit alteration of another degree, or of the ninth
    [InlineData("C7alt(#9)", new[] { 60, 64, 68, 70, 73, 75 })]
    [InlineData("C7(alt,#9)", new[] { 60, 64, 68, 70, 73, 75 })]
    [InlineData("C7alt(#11)", new[] { 60, 64, 68, 70, 73, 78 })]
    // sus chords
    [InlineData("C7sus4(b9)", new[] { 60, 65, 67, 70, 73 })]
    [InlineData("C7sus4(b9,#9)", new[] { 60, 65, 67, 70, 73, 75 })]
    [InlineData("C9sus4(b9)", new[] { 60, 65, 67, 70, 73 })]
    [InlineData("G7sus4(b9,#9)", new[] { 67, 72, 74, 77, 80, 82 })]
    [InlineData("C7sus(b9,#9)", new[] { 60, 65, 67, 70, 73, 75 })]
    [InlineData("Csus2(#5)", new[] { 60, 62, 68 })]
    // minor and major-seventh chords with both ninths or both fifths
    [InlineData("Cm7(b9,#9)", new[] { 60, 63, 67, 70, 73, 75 })]
    [InlineData("Cm7(b5,#5)", new[] { 60, 63, 66, 68, 70 })]
    [InlineData("Cm9(b9,#9)", new[] { 60, 63, 67, 70, 73, 75 })]
    [InlineData("Am7(b9,#9)", new[] { 69, 72, 76, 79, 82, 84 })]
    [InlineData("Cmaj7(b9,#9)", new[] { 60, 64, 67, 71, 73, 75 })]
    [InlineData("Cmaj7(#5,b5)", new[] { 60, 64, 66, 68, 71 })]
    [InlineData("CΔ7(#5,b5)", new[] { 60, 64, 66, 68, 71 })]
    [InlineData("Fmaj7(#9,b9)", new[] { 65, 69, 72, 76, 78, 80 })]
    [InlineData("Cmmaj7(b9,#9)", new[] { 60, 63, 67, 71, 73, 75 })]
    [InlineData("C-7(b9,#9)", new[] { 60, 63, 67, 70, 73, 75 })]
    // half-diminished, diminished and augmented under altered ninths
    [InlineData("Cm7b5(b9,#9)", new[] { 60, 63, 66, 70, 73, 75 })]
    [InlineData("Cø7(b9)", new[] { 60, 63, 66, 70, 73 })]
    [InlineData("Cø7omit5", new[] { 60, 63, 70 })]
    [InlineData("C°7(b9,#9)", new[] { 60, 63, 66, 69, 73, 75 })]
    [InlineData("Cdim7(b9)", new[] { 60, 63, 66, 69, 73 })]
    [InlineData("C+7(b5,#5)", new[] { 60, 64, 66, 68, 70 })]
    [InlineData("Caug7(b9,#9)", new[] { 60, 64, 68, 70, 73, 75 })]
    // power chords carry both fifths
    [InlineData("C5(b5,#5)", new[] { 60, 66, 68 })]
    [InlineData("G5(#5,b5)", new[] { 67, 73, 75 })]
    // Unicode and "+" accidentals
    [InlineData("C7(♭9,♯9)", new[] { 60, 64, 67, 70, 73, 75 })]
    [InlineData("C♯7(♭9,♯9)", new[] { 61, 65, 68, 71, 74, 76 })]
    [InlineData("D♭7(♭5,♯5,♭9,♯9)", new[] { 61, 65, 67, 69, 71, 74, 76 })]
    [InlineData("A♭7♭9♯9", new[] { 68, 72, 75, 78, 81, 83 })]
    [InlineData("E7(♯5,♭5,♯9)", new[] { 64, 68, 70, 72, 74, 79 })]
    [InlineData("G7(b9,+9)", new[] { 67, 71, 74, 77, 80, 82 })]
    [InlineData("G7b9+9", new[] { 67, 71, 74, 77, 80, 82 })]
    [InlineData("A7(+5,b5)", new[] { 69, 73, 75, 77, 79 })]
    // two groups, a group after a bare alteration, spaces, a repeated alteration
    [InlineData("C7(b5)(#5)", new[] { 60, 64, 66, 68, 70 })]
    [InlineData("C7(b9)(#9)", new[] { 60, 64, 67, 70, 73, 75 })]
    [InlineData("G7b9(#9)", new[] { 67, 71, 74, 77, 80, 82 })]
    [InlineData("C7( b9 , #9 )", new[] { 60, 64, 67, 70, 73, 75 })]
    [InlineData("C7(#9,#9)", new[] { 60, 64, 67, 70, 75 })]
    // the natural extension gives way to the alteration of its own degree
    [InlineData("C9(#9)", new[] { 60, 64, 67, 70, 75 })]
    [InlineData("C11(#11)", new[] { 60, 64, 67, 70, 74, 78 })]
    [InlineData("C13(b13)", new[] { 60, 64, 67, 70, 74, 77, 80 })]
    public void TheSymbolSpellsWhatAMusicianWrote(string symbol, int[] expected)
    {
        Assert.Equal(expected, Parse(symbol));
    }

    [Fact]
    public void BothRoadsToTheSamePitchesAgree()
    {
        foreach (var symbol in new[] { "G7b9#9", "Cm7(b5,#5)", "C7(b9,#9)/E", "C5(b5,#5)" })
        {
            Assert.True(ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches));
            Assert.Equal(Parse(symbol), pitches.Order().ToArray());
            Assert.True(ProgressionAdvisor.TryParseChordSymbol(symbol, out pitches, out var errors));
            Assert.Empty(errors);
            Assert.Equal(Parse(symbol), pitches.Order().ToArray());
        }
    }

    [Fact]
    public void TheReportHearsBothFifths()
    {
        var notes = ProgressionAdvisor.Analyze(["C7(b5,#5)"]).Chords[0].Notes;

        Assert.Contains(notes, n => n is "Gb" or "F#");
        Assert.Contains(notes, n => n is "G#" or "Ab");
        Assert.DoesNotContain("G", notes);
    }

    [Fact]
    public void TheSameAlterationsWrittenThreeWaysAreOneChord()
    {
        // One group, two groups, and no parentheses at all, on roots the fix's own property
        // does not visit and on the 13 extension it does not reach.
        foreach (var root in new[] { "Db", "E", "G", "Ab", "B" })
        {
            foreach (var extension in new[] { "7", "9", "13" })
            {
                foreach (var (first, second) in new[] { ("b9", "#9"), ("b5", "#5"), ("#9", "b13"), ("b5", "#11") })
                {
                    var oneGroup = Parse($"{root}{extension}({first},{second})");
                    var twoGroups = Parse($"{root}{extension}({first})({second})");
                    var bare = Parse($"{root}{extension}{first}{second}");
                    var reversed = Parse($"{root}{extension}({second},{first})");

                    Assert.Equal(oneGroup, twoGroups);
                    Assert.Equal(oneGroup, bare);
                    Assert.Equal(oneGroup, reversed);
                }
            }
        }
    }

    [Fact]
    public void TheShorthandsThatAlterTheFifthKeepTheirSeventh()
    {
        // The seventh of a diminished chord is the one thing that reads the altered fifth:
        // dim7 is the diminished seventh unless the fifth was altered by "ø"/"m7b5".
        Assert.Equal([71, 74, 77, 80], Parse("Bdim7"));
        Assert.Equal([71, 74, 77, 81], Parse("Bø7"));
        Assert.Equal([71, 74, 77, 81], Parse("Bm7b5"));
        Assert.Equal(Parse("F#ø7"), Parse("F#m7b5"));
        Assert.Equal(Parse("F#ø7"), Parse("F#halfdim7"));
        Assert.Equal([66, 70, 74, 76, 79], Parse("F#7alt"));
    }
}

// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The chord-symbol parser's rule is that it refuses what it cannot spell rather than spelling
/// something else — and four edges of the builder still dropped a note that was written, with
/// nothing to say so. A power chord ignored every alteration but its fifth, so "C5(b9)" was a
/// bare C5. An altered fifth displaced the diminished or augmented fifth the triad's own quality
/// named, so "Caug7(b5)" came back as C7b5 with the augmented fifth gone. The b5 of "Cdim7(b5)" —
/// redundant on a chord whose fifth is already flat — was read as the half-diminished mark and
/// flipped the diminished seventh to a minor one, so Cdim7(b5) was Cø7. And the add of
/// "C7(b9)add9" went in before the alterations, which took the added D out with the natural.
/// A polychord, besides, named a pitch its layers shared twice: C9|D had two D5s.
/// </summary>
/// <remarks>
/// Every symbol here parsed before the fix and came back with a different set of pitches, so each
/// row failed on the old answer; the rows that pin an unchanged answer (C5, C7(b5,#5), Cø7,
/// C9(b9)) guard the convention the fix had to keep.
/// </remarks>
public class NothingWrittenInASymbolIsDroppedTests
{
    private static int[] Parse(string symbol) => [.. ProgressionAdvisor.ParseChordSymbol(symbol).Order()];

    [Theory]
    // (a) a power chord under an alteration or an add: root, fifth, and the note that was written
    [InlineData("C5(b9)", new[] { 60, 67, 73 })]
    [InlineData("C5add9", new[] { 60, 67, 74 })]
    [InlineData("C5(#11)", new[] { 60, 67, 78 })]
    [InlineData("C5(b13)", new[] { 60, 67, 80 })]
    [InlineData("C5(b9,#9)", new[] { 60, 67, 73, 75 })]
    [InlineData("G5(b9)", new[] { 67, 74, 80 })]
    [InlineData("C5(b9)add11", new[] { 60, 67, 73, 77 })]
    [InlineData("C(5)(b9)", new[] { 60, 67, 73 })]
    // the fifth of a power chord was already honoured, and still is
    [InlineData("C5", new[] { 60, 67 })]
    [InlineData("C5(b5)", new[] { 60, 66 })]
    [InlineData("C5(b5,#5)", new[] { 60, 66, 68 })]
    public void APowerChordTakesAnAlterationLikeAnyOtherChord(string symbol, int[] expected)
    {
        Assert.Equal(expected, Parse(symbol));
    }

    [Theory]
    // (b) a redundant b5 on a diminished seventh is a diminished seventh
    [InlineData("Cdim7(b5)", new[] { 60, 63, 66, 69 })]
    [InlineData("C°7(b5)", new[] { 60, 63, 66, 69 })]
    [InlineData("Bdim7b5", new[] { 71, 74, 77, 80 })]
    [InlineData("Cdim(b5)", new[] { 60, 63, 66 })]
    // a second fifth written on a diminished or augmented chord is a second fifth, as on C7(b5,#5)
    [InlineData("Cdim7(#5)", new[] { 60, 63, 66, 68, 69 })]
    [InlineData("Caug7(b5)", new[] { 60, 64, 66, 68, 70 })]
    [InlineData("C+7(b5)", new[] { 60, 64, 66, 68, 70 })]
    [InlineData("Caug(b5)", new[] { 60, 64, 66, 68 })]
    [InlineData("Caug7(#5)", new[] { 60, 64, 68, 70 })]
    // the shorthands that alter the fifth keep their seventh
    [InlineData("Cø7", new[] { 60, 63, 66, 70 })]
    [InlineData("Chalfdim7", new[] { 60, 63, 66, 70 })]
    [InlineData("Cm7b5", new[] { 60, 63, 66, 70 })]
    [InlineData("Cm7(b5)", new[] { 60, 63, 66, 70 })]
    [InlineData("C7(b5,#5)", new[] { 60, 64, 66, 68, 70 })]
    [InlineData("C7(#5)", new[] { 60, 64, 68, 70 })]
    public void OnlyThePerfectFifthGivesWayToAnAlteredOne(string symbol, int[] expected)
    {
        Assert.Equal(expected, Parse(symbol));
    }

    [Fact]
    public void TheHalfDiminishedMarkIsTheOnlyThingThatMakesADiminishedSeventhMinor()
    {
        Assert.Equal(Parse("Cdim7"), Parse("Cdim7(b5)"));
        Assert.Equal(Parse("C7(b5,#5)"), Parse("Caug7(b5)"));
        Assert.NotEqual(Parse("Cdim7"), Parse("Cø7"));
        Assert.Equal(Parse("Cø7"), Parse("Cm7b5"));
    }

    [Theory]
    // (c) an explicit add is heard beside an alteration of its own degree
    [InlineData("C7(b9)add9", new[] { 60, 64, 67, 70, 73, 74 })]
    [InlineData("Cadd9(b9)", new[] { 60, 64, 67, 73, 74 })]
    [InlineData("C7add9(b9,#9)", new[] { 60, 64, 67, 70, 73, 74, 75 })]
    [InlineData("C7(#11)add11", new[] { 60, 64, 67, 70, 77, 78 })]
    [InlineData("C7(b13)add13", new[] { 60, 64, 67, 70, 80, 81 })]
    [InlineData("C2(b9)", new[] { 60, 64, 67, 73, 74 })]
    [InlineData("C6/9(b9)", new[] { 60, 64, 67, 69, 73, 74 })]
    [InlineData("G7(b9)add9", new[] { 67, 71, 74, 77, 80, 81 })]
    // the natural of the extension chain still gives way to the alteration: that is the convention
    [InlineData("C9(b9)", new[] { 60, 64, 67, 70, 73 })]
    [InlineData("C9(b9,#9)", new[] { 60, 64, 67, 70, 73, 75 })]
    [InlineData("C11(#11)", new[] { 60, 64, 67, 70, 74, 78 })]
    [InlineData("C13(b13)", new[] { 60, 64, 67, 70, 74, 77, 80 })]
    public void AnExplicitAddSurvivesAnAlterationOfItsDegree(string symbol, int[] expected)
    {
        Assert.Equal(expected, Parse(symbol));
    }

    [Theory]
    // (d) a pitch two layers of a polychord share is one pitch
    [InlineData("C9|D", new[] { 60, 64, 67, 70, 74, 78, 81 })]
    [InlineData("C7(b9,#9)|Db", new[] { 60, 64, 67, 70, 73, 75, 77, 80 })]
    [InlineData("C|G|D", new[] { 60, 64, 67, 79, 83, 86, 90, 93 })]
    [InlineData("C|G", new[] { 60, 64, 67, 79, 83, 86 })]
    [InlineData("Cmaj7|Dm7", new[] { 60, 64, 67, 71, 74, 77, 81, 84 })]
    public void APolychordNamesEachPitchOnce(string symbol, int[] expected)
    {
        var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);

        Assert.Equal(expected, pitches);
        Assert.Equal(pitches.Length, pitches.Distinct().Count());
    }

    [Fact]
    public void EverySymbolNamesEachPitchOnce()
    {
        // The whole grid the parity table asks, plus slash chords and polychords whose layers
        // collide, through both public roads.
        string[] roots = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
        string[] suffixes = ["", "m", "7", "maj7", "m7", "9", "m9", "13", "7(b9,#9)", "7(b5,#5)", "5(b9)", "dim7(#5)", "aug7(b5)", "6/9", "7(b9)add9"];
        string[] compound = ["C9|D", "C7(b9,#9)|Db", "C|G|D", "C13|G13|D13|A13", "Am7/C", "C/C", "C7/Bb", "C/E|G", "Dm7|G7|Cmaj7"];

        var symbols = roots.SelectMany(root => suffixes.Select(suffix => root + suffix)).Concat(compound);
        foreach (var symbol in symbols)
        {
            Assert.True(ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches), $"{symbol} did not parse");
            Assert.True(pitches.Length == pitches.Distinct().Count(), $"{symbol} = [{string.Join(", ", pitches)}] names a pitch twice");
            Assert.Equal(pitches, ProgressionAdvisor.ParseChordSymbol(symbol));
        }
    }

    [Theory]
    // (f) a root is a capital: "cm7" is not a chord by any convention a lead sheet relies on
    [InlineData("c")]
    [InlineData("c7")]
    [InlineData("cm7")]
    [InlineData("a")]
    [InlineData("e7")]
    [InlineData("bb7")]
    [InlineData("f#m7")]
    [InlineData("C/e")]
    public void ALowercaseRootIsRefusedOnBothRoads(string symbol)
    {
        Assert.False(ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches));
        Assert.Empty(pitches);
        Assert.False(ProgressionAdvisor.TryParseChordSymbol(symbol, out _, out var errors));
        Assert.NotEmpty(errors);
        Assert.Empty(ProgressionAdvisor.ParseChordSymbol(symbol));

        // Capitalised, the same symbol is a chord.
        var capital = char.ToUpperInvariant(symbol[0]) + symbol[1..];
        Assert.True(ProgressionAdvisor.TryParseChordSymbol(capital.Replace("/e", "/E", StringComparison.Ordinal), out _));
    }
}

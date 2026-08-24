// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// The functional-progression builders' argument guards and the minor-key seventh-chord table.
/// An undefined enum value would otherwise fall through a switch to a plausible default — a
/// progression in a mode nobody asked for.
/// </summary>
public class FunctionalHarmonyGuardTests
{
    private static readonly KeySignature CMajor = new(0, true);
    private static readonly KeySignature AMinor = new(9, false);

    public static TheoryData<string> Builders => ["Circle", "TwoFiveOne", "Turnaround", "ThreeSixTwoFiveOne"];

    private static FunctionalChord[] Build(string name, KeySignature key, DiatonicChordType type, MinorDominantStyle style) =>
        name switch
        {
            "Circle" => FunctionalProgressions.Circle(key, type, style),
            "TwoFiveOne" => FunctionalProgressions.TwoFiveOne(key, type, style),
            "Turnaround" => FunctionalProgressions.Turnaround(key, type, style),
            _ => FunctionalProgressions.ThreeSixTwoFiveOne(key, type, style),
        };

    [Theory]
    [MemberData(nameof(Builders))]
    public void EveryBuilderRejectsAnUndefinedChordType(string builder)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Build(builder, CMajor, (DiatonicChordType)42, MinorDominantStyle.Harmonic));

        Assert.Equal("type", ex.ParamName);
    }

    [Theory]
    [MemberData(nameof(Builders))]
    public void EveryBuilderRejectsAnUndefinedDominantStyle(string builder)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Build(builder, AMinor, DiatonicChordType.Seventh, (MinorDominantStyle)42));

        Assert.Equal("minorDominant", ex.ParamName);
    }

    [Theory]
    [MemberData(nameof(Builders))]
    public void EveryBuilderProducesChordsInTheKeyItWasGiven(string builder)
    {
        var chords = Build(builder, AMinor, DiatonicChordType.Triad, MinorDominantStyle.Natural);

        Assert.NotEmpty(chords);
        Assert.All(chords, c => Assert.Equal(AMinor, c.Key));
        Assert.All(chords, c => Assert.InRange((int)c.RootPitchClass, 0, 11));
    }

    [Fact]
    public void SecondaryDominantsRejectAnUndefinedChordType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FunctionalProgressions.SecondaryDominantTo(CMajor, ScaleDegree.V, (DiatonicChordType)42));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FunctionalProgressions.SecondaryDominants(CMajor, (DiatonicChordType)42));
    }

    // ---------- the minor seventh-chord table ----------

    public static TheoryData<ScaleDegree, ChordQuality> MinorSevenths => new()
    {
        { ScaleDegree.I, ChordQuality.Minor7 },
        { ScaleDegree.Ii, ChordQuality.HalfDim7 },
        { ScaleDegree.Iii, ChordQuality.Major7 },
        { ScaleDegree.Iv, ChordQuality.Minor7 },
        { ScaleDegree.Vi, ChordQuality.Major7 },
        { ScaleDegree.Vii, ChordQuality.Dominant7 },
    };

    [Theory]
    [MemberData(nameof(MinorSevenths))]
    public void MinorKeySeventhChordsHaveTheirDiatonicQualities(ScaleDegree degree, ChordQuality expected)
    {
        var chords = FunctionalProgressions.Circle(AMinor, DiatonicChordType.Seventh, MinorDominantStyle.Natural);

        var matching = chords.Where(c => c.Roman.Degree == degree).ToArray();

        Assert.NotEmpty(matching);
        Assert.All(matching, c => Assert.Equal(expected, c.Roman.Quality));
    }

    [Theory]
    [InlineData(MinorDominantStyle.Harmonic, ChordQuality.Dominant7)]
    [InlineData(MinorDominantStyle.Natural, ChordQuality.Minor7)]
    public void TheMinorDominantFollowsTheStyleAsked(MinorDominantStyle style, ChordQuality expected)
    {
        var chords = FunctionalProgressions.Circle(AMinor, DiatonicChordType.Seventh, style);

        var dominants = chords.Where(c => c.Roman.Degree == ScaleDegree.V).ToArray();

        Assert.NotEmpty(dominants);
        Assert.All(dominants, c => Assert.Equal(expected, c.Roman.Quality));
    }

    [Theory]
    [InlineData(MinorDominantStyle.Harmonic, ChordQuality.Major)]
    [InlineData(MinorDominantStyle.Natural, ChordQuality.Minor)]
    public void TheMinorDominantTriadFollowsTheStyleToo(MinorDominantStyle style, ChordQuality expected)
    {
        var chords = FunctionalProgressions.Circle(AMinor, DiatonicChordType.Triad, style);

        var dominants = chords.Where(c => c.Roman.Degree == ScaleDegree.V).ToArray();

        Assert.NotEmpty(dominants);
        Assert.All(dominants, c => Assert.Equal(expected, c.Roman.Quality));
    }

    // ---------- chord symbols with no conventional spelling ----------

    [Fact]
    public void AQualityWithNoStandardSymbol_IsNamedInWords()
    {
        // Quartal harmony has no chord-symbol spelling, so the symbol falls back to the root
        // plus the quality's name rather than inventing a suffix.
        var chord = new FunctionalChord(
            CMajor,
            new RomanNumeralChord(ScaleDegree.I, ChordQuality.Quartal, HarmonicFunction.Tonic));

        Assert.Equal("C Quartal", chord.Symbol());
    }

    [Fact]
    public void AKnownQualityStillGetsItsSuffix()
    {
        var chord = new FunctionalChord(
            CMajor,
            new RomanNumeralChord(ScaleDegree.Ii, ChordQuality.Minor7, HarmonicFunction.Subdominant));

        Assert.Equal("Dm7", chord.Symbol());
    }
}

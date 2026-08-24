// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// RomanNumeralChord spells a degree into pitch classes and renders it two ways. Its quality
/// tables and rendering switches were largely unexercised — arms that fail quietly, since a
/// wrong interval set still returns pitch classes and a missing suffix still returns a string.
/// </summary>
public class RomanNumeralChordTests
{
    private static readonly KeySignature CMajor = new(0, true);

    private static RomanNumeralChord Chord(ScaleDegree degree, ChordQuality quality) =>
        new(degree, quality, HarmonicFunction.Tonic);

    /// <summary>Every quality that spells a chord, with its interval set above the root.</summary>
    public static TheoryData<ChordQuality, int[]> SpellableQualities() => new()
    {
        { ChordQuality.Major, [0, 4, 7] },
        { ChordQuality.Minor, [0, 3, 7] },
        { ChordQuality.Diminished, [0, 3, 6] },
        { ChordQuality.Augmented, [0, 4, 8] },
        { ChordQuality.Sus2, [0, 2, 7] },
        { ChordQuality.Sus4, [0, 5, 7] },
        { ChordQuality.Power, [0, 7] },
        { ChordQuality.Quartal, [0, 5, 10] },
        { ChordQuality.Major7, [0, 4, 7, 11] },
        { ChordQuality.Minor7, [0, 3, 7, 10] },
        { ChordQuality.Dominant7, [0, 4, 7, 10] },
        { ChordQuality.Dominant7Flat5, [0, 4, 6, 10] },
        { ChordQuality.Diminished7, [0, 3, 6, 9] },
        { ChordQuality.HalfDim7, [0, 3, 6, 10] },
        { ChordQuality.MinorMajor7, [0, 3, 7, 11] },
        { ChordQuality.Augmented7, [0, 4, 8, 10] },
        { ChordQuality.Add9, [0, 4, 7, 2] },
        { ChordQuality.Add11, [0, 4, 7, 5] },
    };

    [Theory]
    [MemberData(nameof(SpellableQualities))]
    public void GetPitchClasses_SpellsEachQualityFromItsRoot(ChordQuality quality, int[] intervals)
    {
        // Degree I in C major roots on C, so the pitch classes are the intervals themselves.
        var pitchClasses = Chord(ScaleDegree.I, quality).GetPitchClasses(CMajor);

        Assert.Equal(intervals.Select(i => (byte)(i % 12)), pitchClasses);
    }

    [Theory]
    [MemberData(nameof(SpellableQualities))]
    public void WritePitchClasses_AgreesWithTheAllocatingOverload(ChordQuality quality, int[] intervals)
    {
        var chord = Chord(ScaleDegree.V, quality);
        var expected = chord.GetPitchClasses(CMajor);

        Span<byte> destination = stackalloc byte[8];
        var written = chord.WritePitchClasses(CMajor, destination);

        Assert.Equal(intervals.Length, written);
        Assert.Equal(expected, destination[..written].ToArray());
    }

    [Theory]
    [MemberData(nameof(SpellableQualities))]
    public void GetPitchClassMask_HasABitForEveryPitchClass(ChordQuality quality, int[] intervals)
    {
        var chord = Chord(ScaleDegree.I, quality);

        var mask = chord.GetPitchClassMask(CMajor);

        foreach (var pc in intervals.Select(i => i % 12).Distinct())
        {
            Assert.True((mask & (1 << pc)) != 0, $"{quality} lost pitch class {pc}");
        }
    }

    [Fact]
    public void WritePitchClasses_DestinationTooSmall_Throws()
    {
        var chord = Chord(ScaleDegree.I, ChordQuality.Major7);
        var tooSmall = new byte[2];

        Assert.Throws<ArgumentException>(() => chord.WritePitchClasses(CMajor, tooSmall));
    }

    [Fact]
    public void UnknownQuality_SpellsNothing_RatherThanGuessing()
    {
        var chord = Chord(ScaleDegree.I, ChordQuality.Unknown);

        Assert.Empty(chord.GetPitchClasses(CMajor));
        Assert.Equal(0, chord.WritePitchClasses(CMajor, new byte[8]));
        Assert.Equal(0, chord.GetPitchClassMask(CMajor));
    }

    [Fact]
    public void Invalid_SpellsNothing()
    {
        var invalid = RomanNumeralChord.Invalid;

        Assert.False(invalid.IsValid);
        Assert.Empty(invalid.GetPitchClasses(CMajor));
        Assert.Equal(0, invalid.WritePitchClasses(CMajor, new byte[8]));
        Assert.Equal(0, invalid.GetPitchClassMask(CMajor));
    }

    [Theory]
    [InlineData(ScaleDegree.I, 0)]
    [InlineData(ScaleDegree.Ii, 2)]
    [InlineData(ScaleDegree.Iii, 4)]
    [InlineData(ScaleDegree.Iv, 5)]
    [InlineData(ScaleDegree.V, 7)]
    [InlineData(ScaleDegree.Vi, 9)]
    [InlineData(ScaleDegree.Vii, 11)]
    public void GetRootPitchClass_FollowsTheDegree(ScaleDegree degree, int expected)
    {
        Assert.Equal((byte)expected, Chord(degree, ChordQuality.Major).GetRootPitchClass(CMajor));
    }

    // ---------- rendering ----------

    [Theory]
    [InlineData(ChordQuality.Major, "I")]
    [InlineData(ChordQuality.Minor, "i")]
    [InlineData(ChordQuality.Diminished, "i°")]
    [InlineData(ChordQuality.Augmented, "I+")]
    [InlineData(ChordQuality.Dominant7, "I7")]
    [InlineData(ChordQuality.Major7, "Imaj7")]
    [InlineData(ChordQuality.Minor7, "i7")]
    [InlineData(ChordQuality.Sus2, "Isus2")]
    [InlineData(ChordQuality.Sus4, "Isus4")]
    [InlineData(ChordQuality.Power, "I5")]
    public void ToRomanNumeral_CarriesTheQuality(ChordQuality quality, string expected)
    {
        Assert.Equal(expected, Chord(ScaleDegree.I, quality).ToRomanNumeral());
    }

    [Fact]
    public void ToRomanNumeral_EveryQualityIsDistinguishable()
    {
        // A quality with no suffix would render identically to a plain major triad, which is
        // how augmented and sus chords once became indistinguishable from "I".
        var rendered = SpellableQualities()
            .Select(row => Chord(ScaleDegree.I, (ChordQuality)row[0]).ToRomanNumeral())
            .ToList();

        Assert.Equal(rendered.Count, rendered.Distinct().Count());
    }

    [Fact]
    public void ToNashville_EveryQualityIsDistinguishable()
    {
        var rendered = SpellableQualities()
            .Select(row => Chord(ScaleDegree.I, (ChordQuality)row[0]).ToNashville())
            .ToList();

        Assert.Equal(rendered.Count, rendered.Distinct().Count());
    }

    [Theory]
    [InlineData(ScaleDegree.I, "1")]
    [InlineData(ScaleDegree.Ii, "2")]
    [InlineData(ScaleDegree.Iii, "3")]
    [InlineData(ScaleDegree.Iv, "4")]
    [InlineData(ScaleDegree.V, "5")]
    [InlineData(ScaleDegree.Vi, "6")]
    [InlineData(ScaleDegree.Vii, "7")]
    public void ToNashville_NumbersTheDegree(ScaleDegree degree, string expected)
    {
        Assert.StartsWith(expected, Chord(degree, ChordQuality.Major).ToNashville(), StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_RendersAsAQuestionMark_NotAsATonic()
    {
        // Invalid's Degree defaults to I; rendering it as "I" is what made chromatic chords
        // read as tonics in progression reports.
        Assert.Contains("?", RomanNumeralChord.Invalid.ToRomanNumeral(), StringComparison.Ordinal);
        Assert.Contains("?", RomanNumeralChord.Invalid.ToNashville(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_CombinesTheNumeralAndTheFunction()
    {
        var text = new RomanNumeralChord(ScaleDegree.V, ChordQuality.Dominant7, HarmonicFunction.Dominant)
            .ToString();

        Assert.Contains("V7", text, StringComparison.Ordinal);
        Assert.Contains("Dominant", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MinorKey_SpellsFromTheMinorScale()
    {
        var aMinor = new KeySignature(9, false);

        // Degree III in A minor roots on C.
        Assert.Equal((byte)0, Chord(ScaleDegree.Iii, ChordQuality.Major).GetRootPitchClass(aMinor));
    }
}

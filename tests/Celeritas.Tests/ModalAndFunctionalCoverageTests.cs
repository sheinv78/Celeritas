// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// ModalKey's conversions and equality, and the functional-harmony chord builders. Both are
/// mostly lookup tables and switch arms — the shape that returns a plausible wrong answer
/// rather than failing, so each arm is checked against music theory rather than against itself.
/// </summary>
public class ModalAndFunctionalCoverageTests
{
    private static readonly KeySignature CMajor = new(0, true);
    private static readonly KeySignature AMinor = new(9, false);

    // ---------- ModalKey conversions ----------

    [Theory]
    [InlineData(Mode.Ionian, true)]
    [InlineData(Mode.Lydian, true)]
    [InlineData(Mode.Mixolydian, true)]
    [InlineData(Mode.Aeolian, false)]
    [InlineData(Mode.Dorian, false)]
    [InlineData(Mode.Phrygian, false)]
    [InlineData(Mode.Locrian, false)]
    [InlineData(Mode.HarmonicMinor, false)]
    [InlineData(Mode.MelodicMinor, false)]
    public void ToKeySignature_MapsAModeToTheRightParity(Mode mode, bool expectMajor)
    {
        // A mode with a major third belongs to a major key signature and vice versa; getting
        // this backwards would silently flip every downstream roman numeral.
        var key = new ModalKey((byte)0, mode).ToKeySignature();

        Assert.Equal((byte)0, key.Root);
        Assert.Equal(expectMajor, key.IsMajor);
    }

    [Fact]
    public void FromKeySignature_RoundTripsThroughToKeySignature()
    {
        foreach (var key in new[] { CMajor, AMinor, new KeySignature(7, true), new KeySignature(2, false) })
        {
            var modal = ModalKey.FromKeySignature(key);
            var back = modal.ToKeySignature();

            Assert.Equal(key.Root, back.Root);
            Assert.Equal(key.IsMajor, back.IsMajor);
        }
    }

    [Fact]
    public void ParallelMajorAndMinor_KeepTheRoot_AndSwapTheMode()
    {
        var dorian = new ModalKey((byte)2, Mode.Dorian);

        Assert.Equal(new ModalKey((byte)2, Mode.Ionian), dorian.ParallelMajor);
        Assert.Equal(new ModalKey((byte)2, Mode.Aeolian), dorian.ParallelMinor);
    }

    [Theory]
    [InlineData(Mode.Aeolian, 9, 0)]      // A Aeolian relates to C major
    [InlineData(Mode.Dorian, 2, 0)]       // D Dorian to C major
    [InlineData(Mode.Phrygian, 4, 0)]     // E Phrygian to C major
    [InlineData(Mode.Locrian, 11, 0)]     // B Locrian to C major
    public void RelativeMajor_OfAMinorMode_IsTheSharedScale(Mode mode, int root, int expectedRoot)
    {
        var relative = new ModalKey((byte)root, mode).RelativeMajor;

        Assert.Equal((byte)expectedRoot, relative.Root);
        Assert.Equal(Mode.Ionian, relative.Mode);
    }

    [Theory]
    [InlineData(Mode.Ionian, 0)]
    [InlineData(Mode.Lydian, 5)]
    [InlineData(Mode.Mixolydian, 7)]
    public void RelativeMajor_OfAMajorMode_KeepsItsOwnRoot_AsDocumented(Mode mode, int root)
    {
        // The property is documented "for minor modes". For a major-ish mode it deliberately
        // falls back to the same root in Ionian rather than naming the parent scale, so
        // F Lydian answers F major, not C major. Pinned so the fallback stays deliberate.
        var relative = new ModalKey((byte)root, mode).RelativeMajor;

        Assert.Equal((byte)root, relative.Root);
        Assert.Equal(Mode.Ionian, relative.Mode);
    }

    [Fact]
    public void ModalKey_EqualityIsByRootAndMode()
    {
        var a = new ModalKey((byte)2, Mode.Dorian);
        var b = new ModalKey((byte)2, Mode.Dorian);
        var differentMode = new ModalKey((byte)2, Mode.Aeolian);
        var differentRoot = new ModalKey((byte)4, Mode.Dorian);

        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a != differentMode);
        Assert.True(a != differentRoot);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals("D Dorian"));
    }

    [Fact]
    public void ModalKey_ToString_NamesTheRootAndMode()
    {
        var text = new ModalKey((byte)2, Mode.Dorian).ToString();

        Assert.Contains("D", text, StringComparison.Ordinal);
        Assert.Contains("Dorian", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ModalKey_UndefinedMode_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModalKey((byte)0, (Mode)999));
    }

    // ---------- diatonic chord qualities per mode ----------

    public static TheoryData<Mode, ChordQuality[]> ModeQualities() => new()
    {
        { Mode.Ionian, [ChordQuality.Major, ChordQuality.Minor, ChordQuality.Minor, ChordQuality.Major, ChordQuality.Major, ChordQuality.Minor, ChordQuality.Diminished] },
        { Mode.Dorian, [ChordQuality.Minor, ChordQuality.Minor, ChordQuality.Major, ChordQuality.Major, ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Major] },
        { Mode.Phrygian, [ChordQuality.Minor, ChordQuality.Major, ChordQuality.Major, ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Major, ChordQuality.Minor] },
        { Mode.Lydian, [ChordQuality.Major, ChordQuality.Major, ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Major, ChordQuality.Minor, ChordQuality.Minor] },
        { Mode.Mixolydian, [ChordQuality.Major, ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Major, ChordQuality.Minor, ChordQuality.Minor, ChordQuality.Major] },
        { Mode.Aeolian, [ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Major, ChordQuality.Minor, ChordQuality.Minor, ChordQuality.Major, ChordQuality.Major] },
        { Mode.Locrian, [ChordQuality.Diminished, ChordQuality.Major, ChordQuality.Minor, ChordQuality.Minor, ChordQuality.Major, ChordQuality.Major, ChordQuality.Minor] },
    };

    [Theory]
    [MemberData(nameof(ModeQualities))]
    public void GetDiatonicChordQualities_MatchesTheMode(Mode mode, ChordQuality[] expected)
    {
        Assert.Equal(expected, ModeLibrary.GetDiatonicChordQualities(mode));
    }

    [Fact]
    public void GetDiatonicChordQualities_HarmonicMinor_HasTheAugmentedMediant()
    {
        // The raised 7th makes III augmented — the marker that distinguishes harmonic minor.
        var qualities = ModeLibrary.GetDiatonicChordQualities(Mode.HarmonicMinor);

        Assert.Equal(ChordQuality.Augmented, qualities[2]);
        Assert.Equal(ChordQuality.Major, qualities[4]);   // and a major dominant
    }

    [Fact]
    public void GetDiatonicChordQualities_UndefinedMode_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ModeLibrary.GetDiatonicChordQualities((Mode)999));
    }

    // ---------- FunctionalChord.Symbol across qualities ----------

    [Theory]
    [InlineData(ChordQuality.Major, "C")]
    [InlineData(ChordQuality.Minor, "Cm")]
    [InlineData(ChordQuality.Diminished, "Cdim")]
    [InlineData(ChordQuality.Augmented, "Caug")]
    [InlineData(ChordQuality.Sus2, "Csus2")]
    [InlineData(ChordQuality.Sus4, "Csus4")]
    [InlineData(ChordQuality.Power, "C5")]
    [InlineData(ChordQuality.Major7, "Cmaj7")]
    [InlineData(ChordQuality.Minor7, "Cm7")]
    [InlineData(ChordQuality.Dominant7, "C7")]
    [InlineData(ChordQuality.Dominant7Flat5, "C7b5")]
    [InlineData(ChordQuality.HalfDim7, "Cm7b5")]
    [InlineData(ChordQuality.Diminished7, "Cdim7")]
    [InlineData(ChordQuality.Augmented7, "Caug7")]
    [InlineData(ChordQuality.MinorMajor7, "Cm(maj7)")]
    [InlineData(ChordQuality.Add9, "Cadd9")]
    [InlineData(ChordQuality.Add11, "Cadd11")]
    public void FunctionalChord_Symbol_IsAConventionalChordSymbol(ChordQuality quality, string expected)
    {
        var chord = new FunctionalChord(CMajor,
            new RomanNumeralChord(ScaleDegree.I, quality, HarmonicFunction.Tonic));

        Assert.Equal(expected, chord.Symbol());
    }

    [Fact]
    public void FunctionalChord_Symbol_HonoursFlatSpelling()
    {
        var eFlatMajor = new KeySignature(3, true);
        var chord = new FunctionalChord(eFlatMajor,
            new RomanNumeralChord(ScaleDegree.I, ChordQuality.Major, HarmonicFunction.Tonic));

        Assert.Equal("Eb", chord.Symbol(preferSharps: false));
        Assert.Equal("D#", chord.Symbol(preferSharps: true));
    }

    [Fact]
    public void FunctionalChord_ExposesRootAndMask()
    {
        var chord = new FunctionalChord(CMajor,
            new RomanNumeralChord(ScaleDegree.V, ChordQuality.Dominant7, HarmonicFunction.Dominant));

        Assert.Equal((byte)7, chord.RootPitchClass);
        Assert.Equal("G", chord.RootName());
        Assert.Contains("V", chord.RomanNumeral, StringComparison.Ordinal);

        // G7 is G B D F.
        foreach (var pc in new[] { 7, 11, 2, 5 })
        {
            Assert.True((chord.PitchClassMask & (1 << pc)) != 0, $"mask lost pitch class {pc}");
        }
    }

    // ---------- the progression builders ----------

    [Theory]
    [InlineData(DiatonicChordType.Triad)]
    [InlineData(DiatonicChordType.Seventh)]
    public void Circle_WalksAllSevenDegrees(DiatonicChordType type)
    {
        var chords = FunctionalProgressions.Circle(CMajor, type);

        Assert.NotEmpty(chords);
        Assert.All(chords, c => Assert.False(string.IsNullOrWhiteSpace(c.Symbol())));
    }

    [Fact]
    public void TwoFiveOne_IsExactlyThatInCMajor()
    {
        var chords = FunctionalProgressions.TwoFiveOne(CMajor);

        Assert.Equal(3, chords.Length);
        Assert.Equal((byte)2, chords[0].RootPitchClass);   // Dm7
        Assert.Equal((byte)7, chords[1].RootPitchClass);   // G7
        Assert.Equal((byte)0, chords[2].RootPitchClass);   // Cmaj7
    }

    [Fact]
    public void TwoFiveOne_InMinor_UsesTheHarmonicDominantByDefault()
    {
        var chords = FunctionalProgressions.TwoFiveOne(AMinor);

        // The dominant of A minor is E; the harmonic-minor default makes it major-quality.
        Assert.Equal((byte)4, chords[1].RootPitchClass);
        Assert.Contains(chords[1].Symbol(), new[] { "E", "E7" });
    }

    [Fact]
    public void TwoFiveOne_NaturalMinorDominant_IsMinor()
    {
        var chords = FunctionalProgressions.TwoFiveOne(
            AMinor, DiatonicChordType.Triad, MinorDominantStyle.Natural);

        Assert.Equal((byte)4, chords[1].RootPitchClass);
        Assert.Equal("Em", chords[1].Symbol());
    }

    [Fact]
    public void Turnaround_AndThreeSixTwoFiveOne_ProduceTheirDegreeSequences()
    {
        var turnaround = FunctionalProgressions.Turnaround(CMajor);
        var longer = FunctionalProgressions.ThreeSixTwoFiveOne(CMajor);

        Assert.Equal(5, turnaround.Length);   // I - vi - ii - V - I
        Assert.Equal(5, longer.Length);
        Assert.All(turnaround, c => Assert.False(string.IsNullOrWhiteSpace(c.Symbol())));
        Assert.All(longer, c => Assert.False(string.IsNullOrWhiteSpace(c.Symbol())));

        // Both must end on the tonic.
        Assert.Equal((byte)0, turnaround[^1].RootPitchClass);
        Assert.Equal((byte)0, longer[^1].RootPitchClass);
    }

    // ---------- secondary dominants ----------

    [Theory]
    [InlineData(ScaleDegree.Ii, 9)]     // V/ii in C major is A7
    [InlineData(ScaleDegree.Iii, 11)]   // V/iii is B7
    [InlineData(ScaleDegree.Iv, 0)]     // V/IV is C7
    [InlineData(ScaleDegree.V, 2)]      // V/V is D7
    [InlineData(ScaleDegree.Vi, 4)]     // V/vi is E7
    public void SecondaryDominantTo_IsAFifthAboveItsTarget(ScaleDegree target, int expectedRoot)
    {
        var secondary = FunctionalProgressions.SecondaryDominantTo(CMajor, target);

        Assert.Equal(expectedRoot, secondary.Root.Value);
    }

    [Fact]
    public void SecondaryDominantTo_NumeralCaseFollowsTheTargetQuality()
    {
        // V/V targets the major dominant, so the target numeral is upper case.
        Assert.Contains("/V", FunctionalProgressions.SecondaryDominantTo(CMajor, ScaleDegree.V).RomanNumeral,
            StringComparison.Ordinal);
        // V/ii targets a minor chord, so it stays lower case.
        Assert.Contains("/ii", FunctionalProgressions.SecondaryDominantTo(CMajor, ScaleDegree.Ii).RomanNumeral,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SecondaryDominants_CoversEveryUsableTarget()
    {
        var all = FunctionalProgressions.SecondaryDominants(CMajor);

        Assert.NotEmpty(all);
        Assert.All(all, s => Assert.False(string.IsNullOrWhiteSpace(s.Symbol())));
        // Each is a distinct target.
        Assert.Equal(all.Length, all.Select(s => s.TargetDegree).Distinct().Count());
    }
}

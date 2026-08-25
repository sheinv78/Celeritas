// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;

namespace Celeritas.Tests;

/// <summary>
/// Harmonizing in a minor key (a different function table from the major one), notes tied
/// across a barline, and the set-theory members the suite had never read back.
/// </summary>
public class MinorHarmonyAndTieTests
{
    private static readonly KeySignature AMinor = new(9, false);
    private static readonly KeySignature CMajor = new(0, true);

    // ---------- harmonizing a minor melody ----------

    [Fact]
    public void AMinorMelodyIsHarmonizedInItsOwnKey()
    {
        NoteEvent[] melody =
        [
            new(69, Rational.Zero, Rational.Quarter),        // A
            new(72, Rational.Quarter, Rational.Quarter),     // C
            new(71, Rational.Half, Rational.Quarter),        // B
            new(69, new Rational(3, 4), Rational.Quarter),   // A
        ];

        var result = new MelodyHarmonizer().Harmonize(melody, AMinor);

        Assert.Equal(AMinor, result.Key);
        Assert.NotEmpty(result.Chords);
        Assert.All(result.Chords, c => Assert.InRange((int)c.Chord.RootPitchClass, 0, 11));
    }

    [Fact]
    public void TheSameMelodyHarmonizesDifferentlyInMajorAndMinor()
    {
        // The function of each degree differs between the modes, so the chord choices should
        // not be identical.
        NoteEvent[] melody =
        [
            new(72, Rational.Zero, Rational.Quarter),
            new(71, Rational.Quarter, Rational.Quarter),
            new(69, Rational.Half, Rational.Quarter),
            new(67, new Rational(3, 4), Rational.Quarter),
        ];

        var harmonizer = new MelodyHarmonizer();
        var inMajor = harmonizer.Harmonize(melody, CMajor);
        var inMinor = harmonizer.Harmonize(melody, AMinor);

        Assert.Equal(inMajor.Chords.Count, inMinor.Chords.Count);
        Assert.Equal(CMajor, inMajor.Key);
        Assert.Equal(AMinor, inMinor.Key);
    }

    [Fact]
    public void AMinorHarmonizationCostsSomethingFinite()
    {
        NoteEvent[] melody =
        [
            new(69, Rational.Zero, Rational.Quarter),
            new(74, Rational.Quarter, Rational.Quarter),
            new(76, Rational.Half, Rational.Quarter),
            new(69, new Rational(3, 4), Rational.Quarter),
        ];

        var result = new MelodyHarmonizer().Harmonize(melody, AMinor);

        Assert.True(float.IsFinite(result.TotalCost));
    }

    // ---------- ties ----------

    [Fact]
    public void ATiedPairSoundsAsOneNote()
    {
        var notes = MusicNotation.Parse("4/4: C4/4~ C4/4 E4/2");

        Assert.Equal(2, notes.Length);
        Assert.Equal(60, notes[0].Pitch);
        Assert.Equal(Rational.Half, notes[0].Duration);
        Assert.Equal(64, notes[1].Pitch);
    }

    [Fact]
    public void AChainOfTiesJoinsIntoOne()
    {
        var notes = MusicNotation.Parse("4/4: C4/4~ C4/4~ C4/4~ C4/4");

        var note = Assert.Single(notes);
        Assert.Equal(Rational.Whole, note.Duration);
    }

    [Fact]
    public void ATieToADifferentPitchDoesNotJoin()
    {
        // A tie only joins the same pitch; anything else is two notes, whatever the notation
        // asks for.
        var notes = MusicNotation.Parse("4/4: C4/4~ E4/4 G4/2");

        Assert.Equal(3, notes.Length);
        Assert.Equal([60, 64, 67], notes.Select(n => n.Pitch));
    }

    [Fact]
    public void ATieAtTheEndOfThePieceStillSounds()
    {
        // Nothing follows the tie, so there is nothing to join it to — the note must not be
        // swallowed waiting for a partner.
        var notes = MusicNotation.Parse("4/4: C4/2 E4/4 G4/4~");

        Assert.Equal(3, notes.Length);
        Assert.Equal(67, notes[^1].Pitch);
        Assert.Equal(Rational.Quarter, notes[^1].Duration);
    }

    [Fact]
    public void TiesInsideAChordJoinPerPitch()
    {
        var notes = MusicNotation.Parse("4/4: [C4 E4]/4 [C4 E4]/4");

        Assert.Equal(4, notes.Length);
    }

    // ---------- set-theory members ----------

    [Fact]
    public void ASetCarriesItsMaskAlongsideItsPitchClasses()
    {
        var result = PitchClassSetAnalyzer.Analyze([60, 64, 67]);

        Assert.Equal(ChordAnalyzer.GetMask([60, 64, 67]), result.Mask);
        Assert.Equal(3, result.Cardinality);
        Assert.Equal([0, 4, 7], result.PitchClasses);
        Assert.Equal("{0,4,7}", result.PitchClassesText);
        Assert.False(string.IsNullOrWhiteSpace(result.NormalOrderText));
    }

    [Fact]
    public void TheIntervalVectorCountsDownwardIntervalsTheSameWayAsUpward()
    {
        // Given descending input the pairwise difference is negative and has to wrap.
        Assert.Equal(
            PitchClassSetAnalyzer.GetIntervalVector([0, 4, 7]),
            PitchClassSetAnalyzer.GetIntervalVector([7, 4, 0]));
    }

    [Fact]
    public void ARepeatedPitchClassAddsNoIntervalClass()
    {
        Assert.Equal(
            PitchClassSetAnalyzer.GetIntervalVector([0, 4, 7]),
            PitchClassSetAnalyzer.GetIntervalVector([0, 4, 7, 0]));
    }

    [Fact]
    public void AnEmptySetHasNoNormalOrderAndNoPrimeForm()
    {
        var result = PitchClassSetAnalyzer.Analyze([]);

        Assert.Equal(0, result.Cardinality);
        Assert.Empty(result.PitchClasses);
        Assert.Empty(result.NormalOrder);
        Assert.Empty(result.PrimeForm);
        Assert.Equal(0, result.Mask);
    }

    [Fact]
    public void APrimeFormAlwaysStartsOnZero()
    {
        foreach (int[] pitches in new[] { new[] { 62, 66, 69 }, [59, 62, 66], [61, 64, 68, 71] })
        {
            var prime = PitchClassSetAnalyzer.Analyze(pitches).PrimeForm;

            Assert.NotEmpty(prime);
            Assert.Equal(0, prime[0]);
            Assert.All(prime, pc => Assert.InRange(pc, 0, 11));
        }
    }
}

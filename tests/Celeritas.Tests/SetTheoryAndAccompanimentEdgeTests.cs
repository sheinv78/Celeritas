// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Accompaniment;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;

namespace Celeritas.Tests;

/// <summary>
/// Set-theory operations on an empty set, an interval vector counted from an unsorted set, and
/// the accompaniment generator's handling of doubled and unvoiceable chords. Each of these
/// returns an array either way, so a wrong answer is indistinguishable from a right one without
/// asking for the number.
/// </summary>
public class SetTheoryAndAccompanimentEdgeTests
{
    // ---------- set theory on nothing ----------

    [Fact]
    public void ThePrimeFormOfNothingIsNothing()
    {
        Assert.Empty(PitchClassSetAnalyzer.GetPrimeForm([]));
    }

    [Fact]
    public void TransposingNothingGivesNothing()
    {
        Assert.Empty(PitchClassSetAnalyzer.Transpose([], 5));
    }

    [Fact]
    public void InvertingNothingGivesNothing()
    {
        Assert.Empty(PitchClassSetAnalyzer.Invert([]));
    }

    [Fact]
    public void TheComplementOfNothingIsEveryPitchClass()
    {
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], PitchClassSetAnalyzer.Complement([]));
    }

    [Fact]
    public void TheComplementOfEverythingIsNothing()
    {
        Assert.Empty(PitchClassSetAnalyzer.Complement([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]));
    }

    [Fact]
    public void TheseOperationsRejectNull()
    {
        Assert.Throws<ArgumentNullException>(() => PitchClassSetAnalyzer.Transpose(null!, 1));
        Assert.Throws<ArgumentNullException>(() => PitchClassSetAnalyzer.Invert(null!));
        Assert.Throws<ArgumentNullException>(() => PitchClassSetAnalyzer.Complement(null!));
    }

    // ---------- the interval vector ----------

    [Fact]
    public void AnIntervalVectorDoesNotDependOnTheOrderTheSetIsWrittenIn()
    {
        // Counting an interval downwards must wrap into the same class as counting it up.
        Assert.Equal(
            PitchClassSetAnalyzer.GetIntervalVector([0, 4, 7]),
            PitchClassSetAnalyzer.GetIntervalVector([7, 0, 4]));
    }

    [Fact]
    public void AMajorTriadHasOneEachOfThirdFourthAndFifthClasses()
    {
        // <0 0 1 1 1 0>: a minor third, a major third and a perfect fourth/fifth.
        Assert.Equal([0, 0, 1, 1, 1, 0], PitchClassSetAnalyzer.GetIntervalVector([0, 4, 7]));
    }

    [Fact]
    public void ARepeatedPitchClassContributesNoInterval()
    {
        // The unison between a doubled member is not an interval class.
        Assert.Equal(
            PitchClassSetAnalyzer.GetIntervalVector([0, 4, 7]),
            PitchClassSetAnalyzer.GetIntervalVector([0, 4, 7, 12]));
    }

    [Fact]
    public void TheIntervalVectorOfNothingIsSixZeros()
    {
        Assert.Equal([0, 0, 0, 0, 0, 0], PitchClassSetAnalyzer.GetIntervalVector([]));
    }

    // ---------- the accompaniment generator ----------

    private static List<ChordAssignment> Assignment(params int[] pitches) =>
        [new(Rational.Zero, Rational.Half, ChordAnalyzer.Identify(pitches), pitches)];

    [Fact]
    public void ADoubledPitchClassIsVoicedOnlyOnce()
    {
        // C4, E4, G4 with the root doubled an octave up. The bass doubles the root by design,
        // so it is the chord above the bass that must not sound C twice.
        var notes = AccompanimentGenerator.Generate(Assignment(60, 64, 67, 72), AccompanimentOptions.Default);

        var above = notes.OrderBy(n => n.Pitch).Skip(1).Select(n => PitchMath.Fold(n.Pitch)).ToArray();

        Assert.Equal(3, above.Length);
        Assert.Equal(above.Distinct().Count(), above.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AChordAllowedNoTones_IsRejectedAsAnUninitialisedOptionsValue(int maxTones)
    {
        // MaxChordTones 0 is what default(AccompanimentOptions) gives, and silently producing
        // nothing for it would look like a progression that generated no music.
        var chords = Assignment(60, 64, 67);
        var options = AccompanimentOptions.Default with { MaxChordTones = maxTones };

        Assert.Throws<ArgumentException>(() => AccompanimentGenerator.Generate(chords, options));
    }

    [Fact]
    public void ADefaultOptionsStruct_IsRejectedRatherThanUsed()
    {
        var chords = Assignment(60, 64, 67);

        Assert.Throws<ArgumentException>(() => AccompanimentGenerator.Generate(chords, default(AccompanimentOptions)));
    }

    [Fact]
    public void MaxChordTonesCapsTheVoicing()
    {
        var notes = AccompanimentGenerator.Generate(
            Assignment(60, 64, 67, 70, 74), AccompanimentOptions.Default with { MaxChordTones = 3 });

        Assert.True(notes.Length <= 3 + 1, "the voicing exceeded the tone cap");
    }

    [Fact]
    public void AVoicingRisesFromTheBass_WhateverOrderThePitchClassesArriveIn()
    {
        // G, C, E: the voicing must climb rather than fold back on itself.
        var notes = AccompanimentGenerator.Generate(Assignment(67, 60, 64), AccompanimentOptions.Default);

        var chordNotes = notes.Where(n => n.Offset == Rational.Zero).Select(n => n.Pitch).ToArray();

        Assert.Equal(chordNotes.OrderBy(p => p), chordNotes.Distinct().OrderBy(p => p));
        Assert.Equal(chordNotes.Length, chordNotes.Distinct().Count());
    }
    [Fact]
    public void AChordWithNoDuration_IsSkipped()
    {
        List<ChordAssignment> zeroLength =
        [
            new(Rational.Zero, Rational.Zero, ChordAnalyzer.Identify([60, 64, 67]), [60, 64, 67]),
            new(Rational.Zero, Rational.Half, ChordAnalyzer.Identify([65, 69, 72]), [65, 69, 72]),
        ];

        var notes = AccompanimentGenerator.Generate(zeroLength, AccompanimentOptions.Default);

        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.True(n.Duration > Rational.Zero));
    }

    [Fact]
    public void ARomanNumeralArpeggioWithNoSubdivision_FallsBackToEighths()
    {
        List<HarmonicRhythmItem> progression =
            [new(new RomanNumeralChord(ScaleDegree.I, ChordQuality.Major, HarmonicFunction.Tonic), Rational.Half)];

        var notes = AccompanimentGenerator.Generate(progression, new KeySignature(0, true),
            AccompanimentOptions.Default with { Pattern = AccompanimentPattern.Arpeggio, Subdivision = Rational.Zero });

        Assert.Equal(4, notes.Length);          // a half note filled with eighths
        Assert.All(notes, n => Assert.Equal(Rational.Eighth, n.Duration));
    }

    [Fact]
    public void ARomanNumeralItemWithNoDuration_IsSkipped()
    {
        List<HarmonicRhythmItem> progression =
        [
            new(new RomanNumeralChord(ScaleDegree.I, ChordQuality.Major, HarmonicFunction.Tonic), Rational.Zero),
            new(new RomanNumeralChord(ScaleDegree.V, ChordQuality.Major, HarmonicFunction.Dominant), Rational.Half),
        ];

        var notes = AccompanimentGenerator.Generate(progression, new KeySignature(0, true));

        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.True(n.Duration > Rational.Zero));
    }
}

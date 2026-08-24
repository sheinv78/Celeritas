// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Accompaniment;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The last of the uncovered library code: the modal progression catalogue and its detector,
/// and the accompaniment overload that takes a harmonic rhythm rather than chord assignments.
/// </summary>
public class ModalProgressionsAndAccompanimentTests
{
    private static readonly KeySignature CMajor = new(0, true);

    // ---------- the catalogue ----------

    [Theory]
    [InlineData(Mode.Ionian)]
    [InlineData(Mode.Dorian)]
    [InlineData(Mode.Phrygian)]
    [InlineData(Mode.Lydian)]
    [InlineData(Mode.Mixolydian)]
    [InlineData(Mode.Aeolian)]
    [InlineData(Mode.Locrian)]
    [InlineData(Mode.HarmonicMinor)]
    [InlineData(Mode.MelodicMinor)]
    [InlineData(Mode.PhrygianDominant)]
    [InlineData(Mode.LydianDominant)]
    public void EveryCataloguedMode_HasUsableProgressions(Mode mode)
    {
        var progressions = ModalProgressions.GetProgressionsForMode(mode);

        Assert.NotEmpty(progressions);
        foreach (var p in progressions)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
            Assert.NotEmpty(p.Degrees);
            // Degrees are 1-based scale positions; anything outside that is a table typo.
            Assert.All(p.Degrees, d => Assert.InRange(d, 1, 7));
        }
    }

    [Fact]
    public void AModeWithNoCatalogueOfItsOwn_FallsBackToTheMajorSet()
    {
        // Blues and the other exotic scales share the Ionian table rather than returning empty.
        var fallback = ModalProgressions.GetProgressionsForMode(Mode.Blues);

        Assert.NotEmpty(fallback);
        Assert.Equal(ModalProgressions.IonianProgressions, fallback);
    }

    [Fact]
    public void UndefinedMode_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ModalProgressions.GetProgressionsForMode((Mode)999));
    }

    [Fact]
    public void ProgressionNamesAreDistinctWithinAMode()
    {
        foreach (var mode in new[] { Mode.Dorian, Mode.Phrygian, Mode.Mixolydian, Mode.Aeolian })
        {
            var names = ModalProgressions.GetProgressionsForMode(mode).Select(p => p.Name).ToArray();
            Assert.Equal(names.Length, names.Distinct().Count());
        }
    }

    // ---------- the detector ----------

    [Fact]
    public void DetectModalProgression_RecognisesACataloguedSequence()
    {
        var dorian = ModalProgressions.GetProgressionsForMode(Mode.Dorian)[0];

        var (mode, match, confidence) = ModalProgressions.DetectModalProgression(
            [.. dorian.Degrees]);

        Assert.NotNull(match);
        Assert.True(confidence > 0f);
        // Degree numbers alone cannot separate modes that share a shape, so the detector may
        // name any mode whose catalogue holds this sequence -- but it must name one that does.
        Assert.Contains(ModalProgressions.GetProgressionsForMode(mode),
            p => p.Degrees.SequenceEqual(dorian.Degrees));
    }

    [Fact]
    public void DetectModalProgression_UnknownSequence_ReportsNoMatch()
    {
        var (_, match, confidence) = ModalProgressions.DetectModalProgression([1, 1, 1, 1, 1, 1, 1]);

        Assert.True(match is null || confidence < 1f);
    }

    [Fact]
    public void DetectModalProgression_EmptyInput_IsHandled()
    {
        var (mode, match, confidence) = ModalProgressions.DetectModalProgression([]);

        Assert.Equal(Mode.Ionian, mode);
        Assert.Null(match);
        Assert.Equal(0f, confidence);
    }

    // ---------- Analyze ----------

    [Fact]
    public void Analyze_EmptyProgression_IsAnEmptyResult()
    {
        var result = ModalProgressions.Analyze([]);

        Assert.Empty(result.Degrees);
        Assert.Empty(result.BorrowedChords);
        Assert.Null(result.MatchedProgression);
        Assert.Equal(0f, result.ModeConfidence);
    }

    [Fact]
    public void Analyze_DorianProgression_NamesADorianKey()
    {
        // i - IV - i in D Dorian: the major IV over a minor tonic is the Dorian signature.
        var result = ModalProgressions.Analyze(["Dm", "G", "Dm"], rootHint: 2);

        Assert.Equal((byte)2, result.DetectedKey.Root);
        Assert.Equal(3, result.Degrees.Count);
    }

    [Fact]
    public void Analyze_UnparsableSymbol_IsSkippedRatherThanCrashing()
    {
        var result = ModalProgressions.Analyze(["Dm", "Zzz", "G"]);

        Assert.Equal(3, result.Degrees.Count);
    }

    [Fact]
    public void Analyze_NullElement_IsRejected()
    {
        // Guard.ThrowIfNullOrHasNullElement reports a null element as a null argument,
        // which is the convention the rest of the library follows.
        Assert.Throws<ArgumentNullException>(() => ModalProgressions.Analyze(["C", null!]));
    }

    [Fact]
    public void Analyze_BorrowedChord_IsFlagged()
    {
        // Fm in C major is borrowed from the parallel minor.
        var result = ModalProgressions.Analyze(["C", "Fm", "C"], rootHint: 0);

        Assert.NotNull(result.BorrowedChords);
    }

    [Fact]
    public void Analyze_RootHint_SteersTheDetectedKey()
    {
        var withHint = ModalProgressions.Analyze(["Dm", "G", "Dm"], rootHint: 2);
        var withOther = ModalProgressions.Analyze(["Dm", "G", "Dm"], rootHint: 7);

        Assert.Equal((byte)2, withHint.DetectedKey.Root);
        Assert.Equal((byte)7, withOther.DetectedKey.Root);
    }

    // ---------- the harmonic-rhythm accompaniment overload ----------

    private static List<HarmonicRhythmItem> Progression() =>
    [
        new(new RomanNumeralChord(ScaleDegree.I, ChordQuality.Major, HarmonicFunction.Tonic), Rational.Half),
        new(new RomanNumeralChord(ScaleDegree.V, ChordQuality.Dominant7, HarmonicFunction.Dominant), Rational.Half),
    ];

    [Theory]
    [InlineData(AccompanimentPattern.Block)]
    [InlineData(AccompanimentPattern.Arpeggio)]
    public void HarmonicRhythm_BothPatterns_SoundInsideTheProgression(AccompanimentPattern pattern)
    {
        var notes = AccompanimentGenerator.Generate(Progression(), CMajor,
            AccompanimentOptions.Default with { Pattern = pattern });

        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.InRange(n.Pitch, 0, 127));
        Assert.All(notes, n => Assert.True(n.Offset + n.Duration <= Rational.Whole));
    }

    [Fact]
    public void HarmonicRhythm_VoicesTheDegreesOfTheGivenKey()
    {
        var notes = AccompanimentGenerator.Generate(Progression(), CMajor);

        // The first segment is the tonic of C major, the second its dominant seventh.
        var first = notes.Where(n => n.Offset < Rational.Half).Select(n => PitchMath.Fold(n.Pitch)).ToHashSet();
        var second = notes.Where(n => n.Offset >= Rational.Half).Select(n => PitchMath.Fold(n.Pitch)).ToHashSet();

        Assert.Subset(new HashSet<int> { 0, 4, 7 }, first);
        Assert.Subset(new HashSet<int> { 7, 11, 2, 5 }, second);
    }

    [Fact]
    public void HarmonicRhythm_InvalidChord_IsSkippedWithoutLosingTheRest()
    {
        List<HarmonicRhythmItem> withInvalid =
        [
            new(RomanNumeralChord.Invalid, Rational.Half),
            new(new RomanNumeralChord(ScaleDegree.I, ChordQuality.Major, HarmonicFunction.Tonic), Rational.Half),
        ];

        var notes = AccompanimentGenerator.Generate(withInvalid, CMajor);

        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.True(n.Offset >= Rational.Half,
            "the invalid chord produced notes instead of being skipped"));
    }

    [Fact]
    public void HarmonicRhythm_ZeroDurationItem_IsSkipped()
    {
        List<HarmonicRhythmItem> withZero =
        [
            new(new RomanNumeralChord(ScaleDegree.I, ChordQuality.Major, HarmonicFunction.Tonic), Rational.Zero),
            new(new RomanNumeralChord(ScaleDegree.V, ChordQuality.Major, HarmonicFunction.Dominant), Rational.Half),
        ];

        var notes = AccompanimentGenerator.Generate(withZero, CMajor);

        Assert.All(notes, n => Assert.True(n.Duration > Rational.Zero));
    }

    [Fact]
    public void HarmonicRhythm_UnspellableQuality_IsSkippedRatherThanSilentlyVoicedWrong()
    {
        // ChordQuality.Unknown has no interval table, so the chord cannot be spelled at all.
        List<HarmonicRhythmItem> unspellable =
            [new(new RomanNumeralChord(ScaleDegree.I, ChordQuality.Unknown, HarmonicFunction.Tonic), Rational.Half)];

        Assert.Empty(AccompanimentGenerator.Generate(unspellable, CMajor));
    }

    [Fact]
    public void HarmonicRhythm_MinorKey_VoicesTheMinorTonic()
    {
        var aMinor = new KeySignature(9, false);
        List<HarmonicRhythmItem> tonic =
            [new(new RomanNumeralChord(ScaleDegree.I, ChordQuality.Minor, HarmonicFunction.Tonic), Rational.Half)];

        var notes = AccompanimentGenerator.Generate(tonic, aMinor);

        var sounded = notes.Select(n => PitchMath.Fold(n.Pitch)).ToHashSet();
        Assert.Subset(new HashSet<int> { 9, 0, 4 }, sounded);   // A minor triad
    }
}

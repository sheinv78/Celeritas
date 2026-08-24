// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The rest of the harmonic-colour analyzer: suspensions, chromatic labelling, modal-turn
/// coalescing, the colourfulness bands, and the defensive sort of an out-of-order melody.
/// </summary>
public class HarmonicColorCoverageTests
{
    private static readonly KeySignature CMajor = new(0, true);
    private static readonly KeySignature AMinor = new(9, false);

    private static NoteEvent Eighth(int pitch, int index) => new(pitch, new Rational(index, 8), Rational.Eighth);

    // ---------- suspension ----------

    [Fact]
    public void AToneRepeatedIntoANewChord_ResolvingDownByStep_IsASuspension()
    {
        // C4 is a chord tone under C, sounds again under G where it is not, and falls to B3.
        // The repeat lands inside the G rather than on its downbeat: a note that starts
        // exactly at the chord change is read as an appoggiatura instead, and that arm is
        // tested separately.
        NoteEvent[] melody = [Eighth(60, 0), Eighth(60, 2), Eighth(59, 3)];
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero), ("G", new Rational(1, 8))];

        var events = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor).MelodicHarmony;

        Assert.Equal(MelodicHarmonyEventType.Suspension, events[1].Type);
        Assert.Equal("Suspension-like: held tone resolves down", events[1].Description);
    }

    [Fact]
    public void TheSameShapeResolvingUpward_IsNotASuspension()
    {
        // A suspension resolves down, by definition.
        NoteEvent[] melody = [Eighth(60, 0), Eighth(60, 2), Eighth(62, 3)];
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero), ("G", new Rational(1, 8))];

        var events = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor).MelodicHarmony;

        Assert.NotEqual(MelodicHarmonyEventType.Suspension, events[1].Type);
    }

    [Fact]
    public void ANoteStartingOnTheChordChange_IsAnAppoggiatura_NotASuspension()
    {
        NoteEvent[] melody = [Eighth(60, 0), Eighth(60, 1), Eighth(59, 2)];
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero), ("G", new Rational(1, 8))];

        var events = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor).MelodicHarmony;

        Assert.Equal(MelodicHarmonyEventType.Appoggiatura, events[1].Type);
    }

    // ---------- chromatic labelling ----------

    [Theory]
    [InlineData(61, "b2")]
    [InlineData(63, "b3")]
    [InlineData(66, "#4")]
    [InlineData(68, "b6")]
    [InlineData(70, "b7")]
    public void ChromaticNotesInAMajorKey_AreLabelledAsFlattenedDegrees(int pitch, string alteration)
    {
        NoteEvent[] melody = [Eighth(60, 0), Eighth(pitch, 1)];
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero)];

        var result = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor);

        var chromatic = Assert.Single(result.ChromaticNotes);
        Assert.Equal(pitch, chromatic.Pitch);
        Assert.Equal(alteration, chromatic.Alteration);
    }

    [Theory]
    [InlineData(61, "#3")]        // C# is the raised third of A minor
    [InlineData(63, "#4/b5")]     // D#
    [InlineData(66, "#6")]        // F#
    [InlineData(68, "#7")]        // G#
    public void ChromaticNotesInAMinorKey_AreLabelledAsRaisedDegrees(int pitch, string alteration)
    {
        NoteEvent[] melody = [Eighth(57, 0), Eighth(pitch, 1)];
        (string Chord, Rational Start)[] chords = [("Am", Rational.Zero)];

        var result = HarmonicColorAnalyzer.Analyze(melody, chords, AMinor);

        var chromatic = Assert.Single(result.ChromaticNotes);
        Assert.Equal(alteration, chromatic.Alteration);
    }

    [Fact]
    public void AnAlterationWithNoCommonName_IsJustCalledChromatic()
    {
        // The second degree is diatonic in both modes, so a D over A minor is not chromatic
        // at all; the "chromatic" fallback is for intervals the table has no name for.
        NoteEvent[] melody = [Eighth(57, 0), Eighth(62, 1)];
        (string Chord, Rational Start)[] chords = [("Am", Rational.Zero)];

        var result = HarmonicColorAnalyzer.Analyze(melody, chords, AMinor);

        Assert.Empty(result.ChromaticNotes);
    }

    // ---------- out-of-order input ----------

    [Fact]
    public void AMelodyGivenOutOfOrder_IsAnalyzedInTimeOrder()
    {
        NoteEvent[] shuffled = [Eighth(64, 2), Eighth(60, 0), Eighth(62, 1)];
        NoteEvent[] ordered = [Eighth(60, 0), Eighth(62, 1), Eighth(64, 2)];
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero)];

        var fromShuffled = HarmonicColorAnalyzer.Analyze(shuffled, chords, CMajor).MelodicHarmony;
        var fromOrdered = HarmonicColorAnalyzer.Analyze(ordered, chords, CMajor).MelodicHarmony;

        Assert.Equal(fromOrdered.Select(e => e.Offset), fromShuffled.Select(e => e.Offset));
        Assert.Equal(fromOrdered.Select(e => e.Type), fromShuffled.Select(e => e.Type));
        // The middle note is only a passing tone once the notes are in the right order.
        Assert.Equal(MelodicHarmonyEventType.PassingTone, fromShuffled[1].Type);
    }

    // ---------- modal turns ----------

    [Fact]
    public void ARunOfBorrowedChords_IsReportedAsOneModalTurn_NotOnePerWindow()
    {
        // C - Bb - F - Gm - Bb - F: every pitch fits C Mixolydian, only the Bb falls outside
        // C major. Six chords give three overlapping four-chord windows, and they must
        // coalesce into a single segment rather than being reported three times.
        (string Chord, Rational Start)[] chords =
        [
            ("C", Rational.Zero),
            ("Bb", new Rational(1, 4)),
            ("F", new Rational(2, 4)),
            ("Gm", new Rational(3, 4)),
            ("Bb", new Rational(4, 4)),
            ("F", new Rational(5, 4)),
        ];

        NoteEvent[] melody = [Eighth(60, 0)];

        var result = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor);

        var turn = Assert.Single(result.ModalTurns);
        Assert.Equal(0, turn.StartChordIndex);
        Assert.Equal(chords.Length - 1, turn.EndChordIndex);
        Assert.True(turn.Confidence > 0);
        Assert.NotEqual(0, turn.OutOfKeyPitchClassMask & (1 << 10));   // Bb is the outsider
    }

    [Fact]
    public void APlainDiatonicProgression_TurnsNowhere()
    {
        (string Chord, Rational Start)[] chords =
        [
            ("C", Rational.Zero),
            ("F", new Rational(1, 4)),
            ("G", new Rational(2, 4)),
            ("C", new Rational(3, 4)),
        ];

        NoteEvent[] melody = [Eighth(60, 0)];

        Assert.Empty(HarmonicColorAnalyzer.Analyze(melody, chords, CMajor).ModalTurns);
    }

    [Fact]
    public void NoChordsAtAll_MeansNoModalTurns()
    {
        NoteEvent[] melody = [Eighth(60, 0)];

        Assert.Empty(HarmonicColorAnalyzer.Analyze(
            melody, Array.Empty<(string Chord, Rational Start)>(), CMajor).ModalTurns);
    }

    // ---------- the colourfulness bands ----------

    public static TheoryData<int[], string> ColourBands => new()
    {
        // All chord tones: nothing to notice.
        { [60, 64, 67, 72], "Mostly diatonic and stable." },
        // Diatonic but all off the chord.
        { [62, 65, 69, 71], "Moderately colorful (some non-chord tones / chromaticism)." },
        // Half of them chromatic as well.
        { [61, 63, 62, 69], "Colorful (noticeable chromaticism or modal mixture)." },
        // Every note outside both the key and the chord.
        { [61, 63, 66, 68, 70], "Highly colorful / chromatic (strong modal mixture or altered tones)." },
    };

    [Theory]
    [MemberData(nameof(ColourBands))]
    public void TheDescriptionFollowsTheRating(int[] pitches, string expected)
    {
        var melody = pitches.Select((p, i) => Eighth(p, i)).ToArray();

        (string Chord, Rational Start)[] chords = [("C", Rational.Zero)];

        var result = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor);

        Assert.Equal(expected, result.Description);
        Assert.InRange(result.ColorfulnessRating, 0d, 10d);
    }

    [Fact]
    public void AnEmptyMelodyRatesZero()
    {
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero)];

        var result = HarmonicColorAnalyzer.Analyze(Array.Empty<NoteEvent>(), chords, CMajor);

        Assert.Equal(0d, result.ColorfulnessRating);
        Assert.Equal("Mostly diatonic and stable.", result.Description);
    }
    // ---------- awkward chord timings ----------

    [Fact]
    public void TwoChordsStartingAtTheSameMoment_StillGiveTheFirstADuration()
    {
        // A zero-length chord slice would put every note outside every chord; the analyzer
        // gives it a whole note instead.
        NoteEvent[] melody = [Eighth(60, 0), Eighth(64, 1)];
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero), ("F", Rational.Zero)];

        var events = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor).MelodicHarmony;

        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.True(e.ChordEnd > e.ChordStart));
    }

    [Fact]
    public void ANoteBeforeTheFirstChord_IsStillGivenAChordContext()
    {
        // The melody starts before the harmony does: the walker has to step back rather than
        // reading past the start of the chord list.
        NoteEvent[] melody = [Eighth(60, 0), Eighth(64, 5)];
        (string Chord, Rational Start)[] chords = [("C", new Rational(1, 2)), ("G", Rational.Whole)];

        var result = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor);

        Assert.Equal(2, result.MelodicHarmony.Count);
        Assert.All(result.MelodicHarmony, e => Assert.True(e.ChordEnd > e.ChordStart));
    }

    [Fact]
    public void AnUnparsableChordSymbol_IsNamedRatherThanQuietlyDropped()
    {
        // Silently skipping it would shift every later chord's slice, so the analyzer says
        // which symbol it could not read and where.
        NoteEvent[] melody = [Eighth(60, 0), Eighth(64, 1)];
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero), ("Zzz", Rational.Half)];

        var ex = Assert.Throws<ArgumentException>(() => HarmonicColorAnalyzer.Analyze(melody, chords, CMajor));

        Assert.Contains("Zzz", ex.Message, StringComparison.Ordinal);
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
    }

    // ---------- a modal turn that is not decisive enough ----------

    [Fact]
    public void AModalTurnBelowTheImprovementThreshold_IsNotReported()
    {
        (string Chord, Rational Start)[] chords =
        [
            ("C", Rational.Zero),
            ("Bb", new Rational(1, 4)),
            ("F", new Rational(2, 4)),
            ("Gm", new Rational(3, 4)),
        ];
        NoteEvent[] melody = [Eighth(60, 0)];

        var demanding = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor,
            HarmonicColorAnalysisOptions.Default with { MinModalTurnImprovement = 0.99 });

        Assert.Empty(demanding.ModalTurns);
    }

    [Fact]
    public void AModalTurnBelowTheCoverageThreshold_IsNotReported()
    {
        (string Chord, Rational Start)[] chords =
        [
            ("C", Rational.Zero),
            ("Bb", new Rational(1, 4)),
            ("F", new Rational(2, 4)),
            ("Gm", new Rational(3, 4)),
        ];
        NoteEvent[] melody = [Eighth(60, 0)];

        var demanding = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor,
            HarmonicColorAnalysisOptions.Default with { MinModalTurnCoverage = 1.5 });

        Assert.Empty(demanding.ModalTurns);
    }

    [Fact]
    public void AModalTurnRaisesTheColourfulnessRating()
    {
        (string Chord, Rational Start)[] borrowed =
        [
            ("C", Rational.Zero),
            ("Bb", new Rational(1, 4)),
            ("F", new Rational(2, 4)),
            ("Gm", new Rational(3, 4)),
        ];
        (string Chord, Rational Start)[] diatonic =
        [
            ("C", Rational.Zero),
            ("F", new Rational(1, 4)),
            ("G", new Rational(2, 4)),
            ("C", new Rational(3, 4)),
        ];
        NoteEvent[] melody = [Eighth(60, 0), Eighth(64, 2), Eighth(67, 4), Eighth(72, 6)];

        var withTurn = HarmonicColorAnalyzer.Analyze(melody, borrowed, CMajor);
        var without = HarmonicColorAnalyzer.Analyze(melody, diatonic, CMajor);

        Assert.NotEmpty(withTurn.ModalTurns);
        Assert.True(withTurn.ColorfulnessRating > without.ColorfulnessRating);
    }

    // ---------- what a chromatic event carries ----------

    [Fact]
    public void AChromaticEventNamesTheNoteAndItsPitchClass()
    {
        NoteEvent[] melody = [Eighth(60, 0), Eighth(61, 1)];
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero)];

        var chromatic = Assert.Single(HarmonicColorAnalyzer.Analyze(melody, chords, CMajor).ChromaticNotes);

        Assert.Equal(1, chromatic.PitchClass);
        Assert.Contains("C", chromatic.NoteName, StringComparison.Ordinal);
        Assert.Equal(new Rational(1, 8), chromatic.Offset);
    }
}

// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for the key/modal analysis review fixes: negative-pitch folding,
/// margin-calibrated modulation gating, modulation boundary attribution and typing,
/// and the monophonic modulation fallback.
/// </summary>
public class KeyModalReviewFixTests
{
    // MIDI scale octaves used to build passages (one full scale per whole note of eighths).
    private static readonly int[] CMajorScaleOctave = [60, 62, 64, 65, 67, 69, 71, 72];
    private static readonly int[] GMajorScaleOctave = [67, 69, 71, 72, 74, 76, 78, 79];

    // ---------------------------------------------------------------------------
    // Fix 1: KeyProfiler.AnalyzeModulations folded `note.Pitch % 12` incorrectly for
    // negative pitches (C# `%` keeps the sign), indexing backwards out of the
    // distribution and throwing.
    // ---------------------------------------------------------------------------

    [Fact]
    public void AnalyzeModulations_NegativePitches_FoldInsteadOfCrashing()
    {
        using var buffer = new NoteBuffer(16);
        // C major scale one octave below MIDI 0: pitch classes fold to C D E F G A B C.
        int[] cScaleBelowZero = [-12, -10, -8, -7, -5, -3, -1, 0];
        for (var k = 0; k < cScaleBelowZero.Length; k++)
            buffer.AddNote(cScaleBelowZero[k], new Rational(k, 8), new Rational(1, 8));

        // Old code: distribution[-12 % 12 .. -1 % 12] -> IndexOutOfRangeException.
        var trajectory = KeyProfiler.AnalyzeModulations(buffer, new Rational(1, 1), new Rational(1, 1));

        Assert.NotEmpty(trajectory.Points);
        var result = trajectory.Points[0].Result;
        Assert.Equal(0, result.Key.Root);
        Assert.True(result.Key.IsMajor);
        Assert.True(result.Confidence > 0f);
    }

    // ---------------------------------------------------------------------------
    // Fix 2: KeyTrajectory.DetectModulations required BOTH adjacent windows to clear a
    // 0.3 confidence bar. Confidence is a best-vs-runner-up margin (genuine detections
    // measured at 0.2326 on this exact passage; the window straddling the key change at
    // 0.0084), so the old bar rejected every genuine modulation — and any both-windows
    // gate rejects the straddling window no matter the bar. Ambiguous windows are now
    // skipped instead of vetoing.
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectModulations_ClearCtoGPassage_ExactlyOneModulationNearBoundary()
    {
        // 8 whole notes of C-major scale eighths, then 8 whole notes of G-major scale
        // eighths. Window 2 whole notes, step 1.
        using var buffer = new NoteBuffer(128);
        var eighth = new Rational(1, 8);
        for (var k = 0; k < 64; k++)
            buffer.AddNote(CMajorScaleOctave[k % 8], eighth * k, eighth);
        for (var k = 0; k < 64; k++)
            buffer.AddNote(GMajorScaleOctave[k % 8], eighth * (64 + k), eighth);

        var trajectory = KeyProfiler.AnalyzeModulations(buffer, new Rational(2, 1), new Rational(1, 1));
        var modulations = trajectory.DetectModulations().ToList();

        var modulation = Assert.Single(modulations);
        Assert.Equal(0, modulation.FromKey.Root);
        Assert.True(modulation.FromKey.IsMajor);
        Assert.Equal(7, modulation.ToKey.Root);
        Assert.True(modulation.ToKey.IsMajor);
        // The key change is at position 8; the first confident G window is reported.
        Assert.True(modulation.Position >= new Rational(7, 1) && modulation.Position <= new Rational(9, 1),
            $"Modulation at {modulation.Position}, expected near position 8");
    }

    [Fact]
    public void KeyTrajectory_Points_ArePublicAndChronological()
    {
        using var buffer = new NoteBuffer(8);
        for (var k = 0; k < 8; k++)
            buffer.AddNote(CMajorScaleOctave[k], new Rational(k, 8), new Rational(1, 8));

        var trajectory = KeyProfiler.AnalyzeModulations(buffer, new Rational(1, 2), new Rational(1, 2));

        Assert.NotEmpty(trajectory.Points);
        for (var i = 1; i < trajectory.Points.Count; i++)
            Assert.True(trajectory.Points[i - 1].Position < trajectory.Points[i].Position);
    }

    // ---------------------------------------------------------------------------
    // Fix 6: ModeLibrary.DetectModeWithRoot(IEnumerable<int>, int) indexed the
    // distribution with a signed `pc % 12` and threw on negative pitch classes.
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectModeWithRoot_NegativePitchClasses_FoldLikePositiveOnes()
    {
        int[] cIonianPcs = [0, 2, 4, 5, 7, 9, 11];
        var negativePcs = cIonianPcs.Select(pc => pc - 12); // [-12, -10, ..., -1]

        // Old code: distribution[-10] etc. -> IndexOutOfRangeException.
        var (negKey, negConfidence) = ModeLibrary.DetectModeWithRoot(negativePcs, 0);
        var (posKey, posConfidence) = ModeLibrary.DetectModeWithRoot(cIonianPcs, 0);

        Assert.Equal(posKey.Root, negKey.Root);
        Assert.Equal(posKey.Mode, negKey.Mode);
        Assert.Equal(posConfidence, negConfidence);
        Assert.Equal(Mode.Ionian, negKey.Mode);
    }

    // ---------------------------------------------------------------------------
    // Fix 7: ModeLibrary.ContainsPitch shifted by a signed `pitchClass % 12`;
    // `1 << -5` sets bit 27, so every negative in-scale pitch class answered false.
    // ---------------------------------------------------------------------------

    [Fact]
    public void ContainsPitch_NegativePitchClass_FoldsBeforeShifting()
    {
        var cIonian = new ModalKey(0, Mode.Ionian);

        // -5 folds to 7 (G), which is in C Ionian. Old code always returned false here.
        Assert.True(ModeLibrary.ContainsPitch(cIonian, -5));
        Assert.Equal(ModeLibrary.ContainsPitch(cIonian, 7), ModeLibrary.ContainsPitch(cIonian, -5));

        // -6 folds to 6 (F#), not in C Ionian.
        Assert.False(ModeLibrary.ContainsPitch(cIonian, -6));
    }

    // ---------------------------------------------------------------------------
    // Fix 8: PitchClassSetAnalyzer.Complement had the same signed-shift bug; negative
    // pitch classes set bits >= 12 and the 12-tone complement came back wrong.
    // ---------------------------------------------------------------------------

    [Fact]
    public void Complement_NegativePitchClasses_MatchesFoldedEquivalent()
    {
        // -5 and -1 fold to 7 and 11. Old code produced the full 12-tone aggregate.
        var fromNegative = PitchClassSetAnalyzer.Complement([-5, -1]);
        var fromFolded = PitchClassSetAnalyzer.Complement([7, 11]);

        Assert.Equal(fromFolded, fromNegative);
        Assert.Equal(10, fromNegative.Length);
        Assert.DoesNotContain(7, fromNegative);
        Assert.DoesNotContain(11, fromNegative);
    }

    // ---------------------------------------------------------------------------
    // Fixes 9 + 10: ModulationDetector attributed the modulation boundary to the
    // detection index (lagging up to windowSize chords, truncating the measured
    // new-key duration and biasing toward Tonicization), and ModulationType.PivotChord
    // was never produced because the type was decided before FindPivotChord ran.
    //
    // NOTE on the target key: these asserts once accepted either G major or its relative
    // E minor, because window key detection was a pitch-class-set overlap against major and
    // NATURAL minor masks — identical for relative keys — broken by enumeration order, so
    // purely diatonic G-major evidence came back as E minor. IdentifyKey now separates
    // relatives on pitch-class counts and ModulationDetector skips ambiguous windows, so
    // G major is asserted outright: a regression back to E minor must fail here.
    // ---------------------------------------------------------------------------

    private static void AssertGMajor(KeySignature key)
        => Assert.True(key.Root == 7 && key.IsMajor,
            $"Expected G major, got root {key.Root} {(key.IsMajor ? "major" : "minor")}");

    [Fact]
    public void Analyze_EightCChordsThenEightGChords_BoundaryAtIndex8AndTrueModulation()
    {
        // 8 chords in C major (ending on a plain C triad so the last analysis window
        // holds no old-key-only pitch class), then 8 in G major leading with D7 whose
        // F# makes chord index 8 the first unambiguous new-key sonority.
        int[][] cChords =
        [
            [60, 64, 67], [65, 69, 72], [67, 71, 74, 77], [60, 64, 67],
            [62, 65, 69], [67, 71, 74, 77], [65, 69, 72], [60, 64, 67]
        ];
        int[][] gChords =
        [
            [62, 66, 69, 72], [67, 71, 74], [62, 66, 69, 72], [67, 71, 74],
            [62, 66, 69, 72], [67, 71, 74], [62, 66, 69], [67, 71, 74]
        ];

        var notes = new List<NoteEvent>();
        var spacing = new Rational(1, 2);
        for (var i = 0; i < cChords.Length; i++)
            foreach (var p in cChords[i])
                notes.Add(new NoteEvent(p, spacing * i, spacing));
        for (var i = 0; i < gChords.Length; i++)
            foreach (var p in gChords[i])
                notes.Add(new NoteEvent(p, spacing * (8 + i), spacing));

        var result = ModulationDetector.Analyze(notes.ToArray(), new KeySignature("C", true));

        var modulation = Assert.Single(result.Modulations);
        AssertGMajor(modulation.ToKey);

        // Boundary attribution (fix 10): the new key starts at chord index 8 (offset 4).
        // The old code stamped the detection index instead (offset 7.5, almost a full
        // window late). Allow +/- 1 chord of slack around index 8.
        Assert.True(modulation.Offset >= new Rational(7, 2) && modulation.Offset <= new Rational(9, 2),
            $"Boundary at {modulation.Offset}, expected within one chord of offset 4");

        // Classified as a true modulation, not a tonicization: measured from the real
        // boundary the new-key area is 3.5 whole notes, well past the 2-whole bar.
        Assert.NotEqual(ModulationType.Tonicization, modulation.Type);

        // PivotChord upgrade (fix 9): the interval-based type here is the generic
        // Direct, and a pivot chord exists (e.g. the G triad is V in C and I/III in the
        // new key), so the more specific PivotChord classification must win.
        Assert.Equal(ModulationType.PivotChord, modulation.Type);
        Assert.NotNull(modulation.PivotChord);

        AssertGMajor(result.EndKey);
    }

    // ---------------------------------------------------------------------------
    // Fix 11: ModulationDetector.ExtractChords only kept simultaneous-onset groups of
    // 2+ notes, so monophonic input produced no chords and Analyze silently reported
    // "no modulations". Single onsets now form pseudo-chords when fewer than two real
    // chords exist.
    // ---------------------------------------------------------------------------

    [Fact]
    public void Analyze_MonophonicCtoGMelody_DetectsTheKeyChange()
    {
        // 64 eighth notes: 32 of the C major scale, then 32 of the G major scale.
        var notes = new List<NoteEvent>();
        var eighth = new Rational(1, 8);
        for (var k = 0; k < 32; k++)
            notes.Add(new NoteEvent(CMajorScaleOctave[k % 8], eighth * k, eighth));
        for (var k = 0; k < 32; k++)
            notes.Add(new NoteEvent(GMajorScaleOctave[k % 8], eighth * (32 + k), eighth));

        var startKey = new KeySignature("C", true);
        var result = ModulationDetector.Analyze(notes.ToArray(), startKey);

        // Old behavior: zero chords -> no modulations, EndKey == startKey.
        var modulation = Assert.Single(result.Modulations);
        AssertGMajor(modulation.ToKey);
        AssertGMajor(result.EndKey);

        // The boundary is attributed within the first octave of the G-major area
        // (the first unambiguous new-key note is its F#, at offset 19/4).
        Assert.True(modulation.Offset >= new Rational(4, 1) && modulation.Offset < new Rational(5, 1),
            $"Boundary at {modulation.Offset}, expected within the first G-major octave [4, 5)");
    }

    // ---------------------------------------------------------------------------
    // Fix 4: AnalyzeModulations now validates windowSize like stepSize.
    // ---------------------------------------------------------------------------

    [Fact]
    public void AnalyzeModulations_NonPositiveWindowSize_Throws()
    {
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KeyProfiler.AnalyzeModulations(buffer, Rational.Zero, new Rational(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KeyProfiler.AnalyzeModulations(buffer, new Rational(-1, 4), new Rational(1, 1)));
    }
}

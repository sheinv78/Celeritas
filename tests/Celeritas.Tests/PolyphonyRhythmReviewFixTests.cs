// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for the 2026-08 polyphony/rhythm review fix batch: interval
/// statistics, meter scoring, voice separation with temporal overlap, syncopation,
/// swing pairing, contour plateaus, dissonance counting, and predictor fallback.
/// </summary>
public class PolyphonyRhythmReviewFixTests
{
    // ── IntervalStatistics.IntervalCounts (was always all zeros) ─────────────

    [Fact]
    public void Analyze_ParallelFifths_PopulatesIntervalCounts()
    {
        // C4→D4 against G4→A4: a perfect fifth (interval class 7) at both time points.
        using var buf = new NoteBuffer(4);
        buf.AddNote(60, Rational.Zero, Rational.Quarter);
        buf.AddNote(67, Rational.Zero, Rational.Quarter);
        buf.AddNote(62, Rational.Quarter, Rational.Quarter);
        buf.AddNote(69, Rational.Quarter, Rational.Quarter);

        var result = PolyphonyAnalyzer.Analyze(buf);

        Assert.Equal(2, result.IntervalStats.IntervalCounts[7]);
        Assert.Equal(2, result.IntervalStats.Total);
        Assert.Equal(result.IntervalStats.Total, result.IntervalStats.IntervalCounts.Sum());
    }

    // ── Meter scoring (2/4 dominated 4/4, 6/8 dominated 3/4) ─────────────────

    [Fact]
    public void DetectMeter_StraightQuarters_Prefers44()
    {
        // 12 uniform quarters carry no accent information: every candidate meter
        // fits equally well, and the deterministic preference must pick 4/4 (the
        // old element-wise scoring made 2/4 unbeatable on this input).
        using var buf = new NoteBuffer(12);
        for (int i = 0; i < 12; i++)
            buf.AddNote(60, new Rational(i, 4), Rational.Quarter);

        var result = RhythmAnalyzer.DetectMeter(buf);

        Assert.Equal(TimeSignature.Common, result.TimeSignature);
        Assert.InRange(result.Confidence, 0f, 1f);
    }

    [Fact]
    public void DetectMeter_StraightEighths_Prefers44()
    {
        // 24 uniform eighths: again accent-free, again 4/4 (the old scoring made
        // 6/8 unbeatable because eighths all land on 6/8 "beats").
        using var buf = new NoteBuffer(24);
        for (int i = 0; i < 24; i++)
            buf.AddNote(60, new Rational(i, 8), Rational.Eighth);

        var result = RhythmAnalyzer.DetectMeter(buf);

        Assert.Equal(TimeSignature.Common, result.TimeSignature);
        Assert.InRange(result.Confidence, 0f, 1f);
    }

    [Fact]
    public void DetectMeter_CompoundFigure_Detects68()
    {
        // A real 6/8 figure: dotted-quarter bass on the two compound beats plus
        // eighth-note groups of three with the first of each group louder. The
        // duration and velocity accents fall on 6/8's strong positions.
        using var buf = new NoteBuffer(32);
        for (int m = 0; m < 4; m++)
        {
            var start = new Rational(3 * m, 4);
            buf.AddNote(36, start, new Rational(3, 8), 0.9f);
            buf.AddNote(36, start + new Rational(3, 8), new Rational(3, 8), 0.9f);
            for (int k = 0; k < 6; k++)
                buf.AddNote(72, start + new Rational(k, 8), Rational.Eighth, k % 3 == 0 ? 0.9f : 0.7f);
        }

        var result = RhythmAnalyzer.DetectMeter(buf);

        Assert.Equal(TimeSignature.Compound6, result.TimeSignature);
        Assert.InRange(result.Confidence, 0.5f, 1f);
    }

    [Fact]
    public void DetectMeter_OomPahPahWaltz_Detects34()
    {
        // Oom-pah-pah: long, loud low note on beat 1, chords on beats 2 and 3.
        // 3/4 and 6/8 tie on accent alignment; the preference order breaks the
        // tie toward 3/4.
        using var buf = new NoteBuffer(20);
        for (int m = 0; m < 4; m++)
        {
            var start = new Rational(3 * m, 4);
            buf.AddNote(36, start, new Rational(3, 4), 0.95f);
            buf.AddNote(60, start + Rational.Quarter, Rational.Quarter, 0.7f);
            buf.AddNote(64, start + Rational.Quarter, Rational.Quarter, 0.7f);
            buf.AddNote(60, start + Rational.Half, Rational.Quarter, 0.7f);
            buf.AddNote(64, start + Rational.Half, Rational.Quarter, 0.7f);
        }

        var result = RhythmAnalyzer.DetectMeter(buf);

        Assert.Equal(TimeSignature.Waltz, result.TimeSignature);
        Assert.InRange(result.Confidence, 0.5f, 1f);
    }

    // ── Voice separation: temporal overlap (notes collapsed into one voice) ──

    [Fact]
    public void Separate_OverlappingNotes_OpenSecondVoice()
    {
        // C5 whole note, then E5 entering at beat 2 while C5 still sounds: these
        // cannot be one melodic line. The old assignment ignored overlap and
        // swallowed E5 into the C5 voice.
        using var buf = new NoteBuffer(2);
        buf.AddNote(72, Rational.Zero, new Rational(1, 1));
        buf.AddNote(76, Rational.Quarter, Rational.Quarter);

        var separation = VoiceSeparator.Separate(buf, maxVoices: 4);
        Assert.Equal(2, separation.Voices.Count);

        // And the sounding third must appear in the interval statistics.
        var analysis = PolyphonyAnalyzer.Analyze(buf);
        Assert.True(analysis.IntervalStats.IntervalCounts[4] >= 1);
        Assert.True(analysis.IntervalStats.Total >= 1);
    }

    // ── VoiceSeparatorOptions.PreferStepwise / AllowCrossings (were dead) ────

    [Fact]
    public void Separate_PreferStepwise_ChangesAssignment()
    {
        // C4 followed by a twelfth leap to G5. With PreferStepwise the superlinear
        // leap cost opens a second voice; without it the line stays in one voice.
        using var buf = new NoteBuffer(2);
        buf.AddNote(60, Rational.Zero, Rational.Quarter);
        buf.AddNote(79, Rational.Quarter, Rational.Quarter);

        var stepwise = VoiceSeparator.Separate(buf, 2,
            new VoiceSeparatorOptions { PreferStepwise = true, AllowCrossings = true });
        var plain = VoiceSeparator.Separate(buf, 2,
            new VoiceSeparatorOptions { PreferStepwise = false, AllowCrossings = true });

        Assert.Equal(2, stepwise.Voices.Count);
        Assert.Single(plain.Voices);
    }

    [Fact]
    public void Separate_AllowCrossingsFalse_AvoidsCrossingAssignment()
    {
        // Chord (80, 70, 65) where 70 is a whole note; then 72 at beat 3 while 70
        // still sounds. Nearest free voice is the 65-voice, but taking 72 there
        // crosses above the sounding 70. With crossings disallowed the note goes
        // to the upper (80) voice instead.
        using var buf = new NoteBuffer(4);
        buf.AddNote(80, Rational.Zero, Rational.Quarter);
        buf.AddNote(70, Rational.Zero, new Rational(1, 1));
        buf.AddNote(65, Rational.Zero, Rational.Quarter);
        buf.AddNote(72, Rational.Half, Rational.Quarter);

        var crossing = VoiceSeparator.Separate(buf, 3,
            new VoiceSeparatorOptions { PreferStepwise = false, AllowCrossings = true });
        var noCrossing = VoiceSeparator.Separate(buf, 3,
            new VoiceSeparatorOptions { PreferStepwise = false, AllowCrossings = false });

        Assert.Equal(2, crossing.NoteToVoice[3]);   // joins the 65-voice (crosses 70)
        Assert.Equal(0, noCrossing.NoteToVoice[3]); // joins the 80-voice (no crossing)
    }

    // ── Voice crossings counted in the overflow branch ───────────────────────

    [Fact]
    public void Separate_OverflowSlice_CountsVoiceCrossings()
    {
        // Establish v0=72, v1=60, then an overflow slice (3 notes, 2 voices):
        // 65 goes to v1 (nearest), forcing 58 into v0 below v1's fresh 65 — a
        // crossing — and the overflow note 40 lands below 65 as well.
        using var buf = new NoteBuffer(5);
        buf.AddNote(72, Rational.Zero, Rational.Quarter);
        buf.AddNote(60, Rational.Zero, Rational.Quarter);
        buf.AddNote(65, Rational.Quarter, Rational.Quarter);
        buf.AddNote(58, Rational.Quarter, Rational.Quarter);
        buf.AddNote(40, Rational.Quarter, Rational.Quarter);

        var result = VoiceSeparator.Separate(buf, maxVoices: 2);

        Assert.True(result.VoiceCrossings >= 1);
    }

    // ── Syncopation: weak beats vs strong beats (fix 5) ──────────────────────

    [Fact]
    public void Analyze_WaltzHalfNoteOnBeatTwo_IsNotSyncopated()
    {
        // 3/4: quarter on beat 1, half note on beat 2. The half note ends exactly
        // at the next downbeat and crosses only the WEAK beat 3 — not syncopation.
        using var buf = new NoteBuffer(2);
        buf.AddNote(60, Rational.Zero, Rational.Quarter);
        buf.AddNote(64, Rational.Quarter, Rational.Half);

        var result = RhythmAnalyzer.Analyze(buf, TimeSignature.Waltz);

        var halfNote = result.Events.Single(e => e.Offset == Rational.Quarter);
        Assert.False(halfNote.IsSyncopated);
        Assert.Equal(0, result.Statistics.SyncopatedNotes);
    }

    [Fact]
    public void Analyze_NoteHeldOverDownbeat_IsSyncopated()
    {
        // 3/4: an off-beat note at 5/8 held across the next measure's downbeat.
        using var buf = new NoteBuffer(2);
        buf.AddNote(60, Rational.Zero, Rational.Quarter);
        buf.AddNote(64, new Rational(5, 8), Rational.Quarter);

        var result = RhythmAnalyzer.Analyze(buf, TimeSignature.Waltz);

        var offbeat = result.Events.Single(e => e.Offset == new Rational(5, 8));
        Assert.True(offbeat.IsSyncopated);
    }

    // ── Swing detection with a pickup (fix 6) ────────────────────────────────

    [Fact]
    public void Analyze_SwungPatternWithQuarterPickup_DetectsShuffle()
    {
        // A leading quarter pickup used to invert the even/odd pairing and report
        // "reverse swing" (~0.375). Beat-grid pairing must report the true 0.75.
        using var buf = new NoteBuffer(9);
        buf.AddNote(60, Rational.Zero, Rational.Quarter); // pickup
        var offset = Rational.Quarter;
        for (int i = 0; i < 4; i++)
        {
            buf.AddNote(60, offset, new Rational(3, 16));
            buf.AddNote(60, offset + new Rational(3, 16), new Rational(1, 16));
            offset += Rational.Quarter;
        }

        var result = RhythmAnalyzer.Analyze(buf, TimeSignature.Common);

        Assert.InRange(result.SwingRatio, 0.74f, 0.76f);
        Assert.Equal(GrooveFeel.Shuffle, result.GrooveFeel);
    }

    // ── Contour plateaus (fix 7) ─────────────────────────────────────────────

    [Fact]
    public void MelodyAnalyzer_PlateauPeak_IsArch()
    {
        // C D E E D C: the repeated peak defeated strict neighbor comparison and
        // the melody was classified Static.
        var result = MelodyAnalyzer.Analyze([60, 62, 64, 64, 62, 60]);

        Assert.Equal(MelodicContour.Arch, result.Contour);
    }

    [Fact]
    public void MelodyAnalyzer_PlateauTrough_IsBowl()
    {
        var result = MelodyAnalyzer.Analyze([64, 62, 60, 60, 62, 64]);

        Assert.Equal(MelodicContour.Bowl, result.Contour);
    }

    // ── Held dissonance counted once (fix 8) ─────────────────────────────────

    [Fact]
    public void Analyze_SustainedDissonanceBesideMovingVoice_CountsOneViolation()
    {
        // A sustained minor second (B4 under C5, both whole notes) beside a moving
        // bass line. Every bass onset creates a global time point; the held m2 was
        // re-counted at each of them (3 violations for one dissonance).
        using var buf = new NoteBuffer(6);
        buf.AddNote(72, Rational.Zero, new Rational(1, 1));
        buf.AddNote(71, Rational.Zero, new Rational(1, 1));
        int[] bass = [48, 50, 52, 55];
        for (int i = 0; i < 4; i++)
            buf.AddNote(bass[i], new Rational(i, 4), Rational.Quarter);

        var result = PolyphonyAnalyzer.Analyze(buf);

        Assert.Equal(1, result.Violations.Count(v => v.Type == "Unresolved Dissonance"));
    }

    [Fact]
    public void Analyze_SustainedDissonanceAlone_CountsOneViolation()
    {
        // The same sustained m2 without any moving voice: still one violation
        // (the dissonance never resolves), not zero.
        using var buf = new NoteBuffer(2);
        buf.AddNote(72, Rational.Zero, new Rational(1, 1));
        buf.AddNote(71, Rational.Zero, new Rational(1, 1));

        var result = PolyphonyAnalyzer.Analyze(buf);

        Assert.Equal(1, result.Violations.Count(v => v.Type == "Unresolved Dissonance"));
    }

    // ── RhythmPredictor shorter-context fallback (fix 9) ─────────────────────

    [Fact]
    public void Predict_UnseenFullContextWithKnownSuffix_FallsBackToShorterOrder()
    {
        var predictor = new RhythmPredictor(order: 2, seed: 42);
        predictor.Train([Rational.Quarter, Rational.Quarter, Rational.Quarter, Rational.Quarter, Rational.Quarter]);

        // Full context "1/2|1/4" was never seen; the order-1 suffix "1/4" was.
        var prediction = predictor.Predict([Rational.Half, Rational.Quarter]);

        Assert.True(prediction.ContextFound);
        Assert.Equal(Rational.Quarter, prediction.MostLikely);
        Assert.Equal(0.8f, prediction.Confidence, 3); // certain successor × 0.8 fallback factor
    }

    [Fact]
    public void Predict_FullyUnknownContext_ReportsContextNotFound()
    {
        var predictor = new RhythmPredictor(order: 2, seed: 42);
        predictor.Train([Rational.Quarter, Rational.Quarter, Rational.Quarter, Rational.Quarter, Rational.Quarter]);

        var prediction = predictor.Predict([Rational.Half, Rational.Half]);

        Assert.False(prediction.ContextFound);
        Assert.Equal(Rational.Quarter, prediction.MostLikely); // most common overall
    }

    // ── Texture density time-weighting (fix 10) ──────────────────────────────

    [Fact]
    public void Analyze_WholeNoteChordThenSixteenths_TimeWeightsDensity()
    {
        // A four-voice whole-note chord (1 whole, density 4) followed by eight
        // sixteenths (1/2 whole, density 1): (4·1 + 1·0.5) / 1.5 = 3. The old
        // unweighted per-segment average let the eight short segments outvote
        // the chord (≈1.33).
        using var buf = new NoteBuffer(12);
        buf.AddNote(72, Rational.Zero, new Rational(1, 1));
        buf.AddNote(64, Rational.Zero, new Rational(1, 1));
        buf.AddNote(57, Rational.Zero, new Rational(1, 1));
        buf.AddNote(48, Rational.Zero, new Rational(1, 1));
        for (int k = 0; k < 8; k++)
            buf.AddNote(72, new Rational(16 + k, 16), Rational.Sixteenth);

        var result = PolyphonyAnalyzer.Analyze(buf);

        Assert.Equal(3.0f, result.TextureDensity, 3);
    }

    // ── Backbeat / Waltz patterns can now win (fix 12) ───────────────────────

    [Fact]
    public void Analyze_WaltzQuartersIn34_ReportsWaltzPattern()
    {
        using var buf = new NoteBuffer(6);
        for (int i = 0; i < 6; i++)
            buf.AddNote(60, new Rational(i, 4), Rational.Quarter);

        var result = RhythmAnalyzer.Analyze(buf, TimeSignature.Waltz);

        Assert.Contains(result.PatternMatches, m => m.Pattern.Name == "Waltz");
    }

    [Fact]
    public void Analyze_AccentedBackbeat_ReportsBackbeatPattern()
    {
        // Quarters in 4/4 with velocity accents on beats 2 and 4.
        using var buf = new NoteBuffer(8);
        for (int i = 0; i < 8; i++)
            buf.AddNote(60, new Rational(i, 4), Rational.Quarter, i % 2 == 1 ? 0.9f : 0.5f);

        var result = RhythmAnalyzer.Analyze(buf, TimeSignature.Common);

        Assert.Contains(result.PatternMatches, m => m.Pattern.Name == "Backbeat");
    }

    [Fact]
    public void Analyze_UniformVelocityQuarters_ReportsStraightQuartersNotBackbeat()
    {
        using var buf = new NoteBuffer(8);
        for (int i = 0; i < 8; i++)
            buf.AddNote(60, new Rational(i, 4), Rational.Quarter);

        var result = RhythmAnalyzer.Analyze(buf, TimeSignature.Common);

        Assert.DoesNotContain(result.PatternMatches, m => m.Pattern.Name == "Backbeat");
        Assert.Contains(result.PatternMatches, m => m.Pattern.Name == "Straight Quarters");
    }

    // ── Deterministic chord ordering (fix 13) ────────────────────────────────

    [Fact]
    public void MelodyAnalyzer_ChordInsertionOrder_DoesNotChangeIntervals()
    {
        using var forward = new NoteBuffer(4);
        forward.AddNote(64, Rational.Zero, Rational.Quarter);
        forward.AddNote(60, Rational.Zero, Rational.Quarter);
        forward.AddNote(65, Rational.Quarter, Rational.Quarter);
        forward.AddNote(59, Rational.Quarter, Rational.Quarter);

        using var reversed = new NoteBuffer(4);
        reversed.AddNote(59, Rational.Quarter, Rational.Quarter);
        reversed.AddNote(65, Rational.Quarter, Rational.Quarter);
        reversed.AddNote(60, Rational.Zero, Rational.Quarter);
        reversed.AddNote(64, Rational.Zero, Rational.Quarter);

        var a = MelodyAnalyzer.Analyze(forward);
        var b = MelodyAnalyzer.Analyze(reversed);

        Assert.Equal(
            a.Intervals.Select(iv => iv.Semitones),
            b.Intervals.Select(iv => iv.Semitones));
    }

    // ── RhythmPredictor.GetStats transition counting (fix 14) ────────────────

    [Fact]
    public void GetStats_TenQuartersOrderTwo_CountsEightTransitions()
    {
        var predictor = new RhythmPredictor(order: 2, seed: 42);
        predictor.Train([.. Enumerable.Repeat(Rational.Quarter, 10)]);

        var stats = predictor.GetStats();

        Assert.Equal(8, stats.TotalTransitions); // ten notes, order 2 → 8 observed transitions
        Assert.Equal(1, stats.UniqueContexts);
        Assert.Contains(Rational.Quarter, stats.MostCommonDurations);
    }

    // ── DetectImitation: scale runs are not canons (fix 15) ──────────────────

    [Fact]
    public void DetectImitation_SharedMonotoneScaleRun_IsNotACanon()
    {
        // Both voices walk the same whole-tone steps ([2,2,2]) with a delayed
        // entry — a single repeated interval is not a distinctive motif.
        using var buf = new NoteBuffer(8);
        int[] lo = [48, 50, 52, 54];
        int[] hi = [72, 74, 76, 78];
        for (int i = 0; i < 4; i++)
        {
            buf.AddNote(lo[i], new Rational(i, 1), Rational.Quarter);
            buf.AddNote(hi[i], new Rational(i + 1, 1), Rational.Quarter);
        }

        var result = PolyphonyAnalyzer.DetectImitation(buf);

        Assert.False(result.HasImitation);
    }

    [Fact]
    public void DetectImitation_DelayedDistinctMotif_IsACanon()
    {
        // Distinct motif ([2,2,3]) with the upper voice entering two whole notes
        // later: a genuine canon with a positive delay.
        using var buf = new NoteBuffer(8);
        int[] lo = [48, 50, 52, 55];
        int[] hi = [72, 74, 76, 79];
        for (int i = 0; i < 4; i++)
        {
            buf.AddNote(lo[i], new Rational(i, 1), Rational.Quarter);
            buf.AddNote(hi[i], new Rational(i + 2, 1), Rational.Quarter);
        }

        var result = PolyphonyAnalyzer.DetectImitation(buf);

        Assert.True(result.HasImitation);
        Assert.Equal("Canon", result.Type);
        Assert.Equal(new Rational(2, 1), result.TimeDelay);
    }
}

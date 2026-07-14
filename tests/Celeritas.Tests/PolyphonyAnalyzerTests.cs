// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class PolyphonyAnalyzerTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a 2-voice buffer: lower voice at <paramref name="loPitches"/>,
    /// upper voice at <paramref name="hiPitches"/>; one quarter-note per pitch.</summary>
    private static NoteBuffer TwoVoiceBuffer(int[] loPitches, int[] hiPitches)
    {
        var buf = new NoteBuffer(loPitches.Length + hiPitches.Length);
        for (int i = 0; i < loPitches.Length; i++)
            buf.AddNote(loPitches[i], new Rational(i, 4), Rational.Quarter);
        for (int i = 0; i < hiPitches.Length; i++)
            buf.AddNote(hiPitches[i], new Rational(i, 4), Rational.Quarter);
        return buf;
    }

    // ── CheckCounterpointRules ────────────────────────────────────────────────

    [Fact]
    public void CheckCounterpointRules_ParallelFifths_AreDetected()
    {
        // C4→D4 (lower) vs G4→A4 (upper): both move up by M2 while staying a P5 apart
        using var buf = TwoVoiceBuffer([60, 62], [67, 69]);

        var result = PolyphonyAnalyzer.CheckCounterpointRules(buf);

        Assert.True(result.ParallelFifths > 0);
        Assert.True(result.Violations.Count > 0);

        var v = result.Violations.First(x => x.Type == "Parallel Fifths");
        Assert.True(v.Voice1 >= 0);
        Assert.True(v.Voice2 > v.Voice1);
    }

    [Fact]
    public void CheckCounterpointRules_CleanCounterpoint_HasZeroParallelErrors()
    {
        // C4→D4 (lower) vs E5→C5 (upper): contrary motion — no parallel fifths/octaves
        using var buf = TwoVoiceBuffer([60, 62], [76, 72]);

        var result = PolyphonyAnalyzer.CheckCounterpointRules(buf);

        Assert.Equal(0, result.ParallelFifths);
        Assert.Equal(0, result.ParallelOctaves);
        Assert.Equal(0, result.HiddenParallels);
        // VoiceCrossing and SpacingViolations may or may not be zero — just read them
        _ = result.VoiceCrossing;
        _ = result.SpacingViolations;
        Assert.True(result.QualityScore is >= 0f and <= 1f);
    }

    [Fact]
    public void CheckCounterpointRules_Violations_HaveVoiceIndices()
    {
        // Parallel fifths: C4→D4 / G4→A4
        using var buf = TwoVoiceBuffer([60, 62], [67, 69]);

        var result = PolyphonyAnalyzer.CheckCounterpointRules(buf);

        foreach (var violation in result.Violations)
        {
            Assert.True(violation.Voice1 >= 0, "Voice1 must be non-negative");
            Assert.True(violation.Voice2 >= 0, "Voice2 must be non-negative");
        }
    }

    // ── DetectImitation ───────────────────────────────────────────────────────

    [Fact]
    public void DetectImitation_Canon_IsDetected()
    {
        // Both voices always sound together so VoiceSeparator reliably splits them.
        // Lower (C3 octave): C3 D3 E3 G3  — same interval pattern as upper.
        // Upper (C5 octave): C5 D5 E5 G5
        // Intervals in both: [2, 2, 3] → canon detected at unison/octave interval.
        using var buf = new NoteBuffer(8);
        int[] lo = [48, 50, 52, 55]; // C3 D3 E3 G3
        int[] hi = [72, 74, 76, 79]; // C5 D5 E5 G5
        for (int i = 0; i < 4; i++)
        {
            buf.AddNote(lo[i], new Rational(i, 1), Rational.Quarter);
            buf.AddNote(hi[i], new Rational(i, 1), Rational.Quarter);
        }

        var result = PolyphonyAnalyzer.DetectImitation(buf);

        Assert.True(result.HasImitation);
        Assert.Equal("Canon", result.Type);
        // Voices are 2 octaves apart (|48-72|=24 or |72-48|=-24)
        Assert.NotEqual(0, result.Interval);
        // Both voices start simultaneously → delay = 0
        Assert.Equal(Rational.Zero, result.TimeDelay);
        Assert.NotEmpty(result.VoicesInvolved);
        Assert.Equal(2, result.VoicesInvolved.Count);
    }

    [Fact]
    public void DetectImitation_NoImitation_ReturnsFalse()
    {
        // Two completely different, unrelated voices
        using var buf = TwoVoiceBuffer(
            [60, 63, 65, 68],   // lower: random
            [72, 71, 69, 67]);  // upper: descending

        var result = PolyphonyAnalyzer.DetectImitation(buf);

        Assert.False(result.HasImitation);
        _ = result.Type;
        _ = result.Interval;
        _ = result.TimeDelay;
        _ = result.VoicesInvolved;
    }

    // ── Analyze ───────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_TwoVoices_ReturnsIntervalsAndMotions()
    {
        // C4+G4 → D4+A4 (parallel fifths, two time points)
        using var buf = TwoVoiceBuffer([60, 62], [67, 69]);

        var result = PolyphonyAnalyzer.Analyze(buf);

        // Intervals and Motions must be populated
        Assert.NotEmpty(result.Intervals);
        Assert.NotEmpty(result.Motions);

        // Each interval has valid voice indices
        foreach (var interval in result.Intervals)
        {
            Assert.True(interval.Voice1 >= 0);
            Assert.True(interval.Voice2 >= 0);
        }

        // Each motion has valid voice indices
        foreach (var motion in result.Motions)
        {
            Assert.True(motion.Voice1 >= 0);
            Assert.True(motion.Voice2 >= 0);
        }
    }

    [Fact]
    public void Analyze_SingleVoice_ReturnsEmptyIntervalsAndMotions()
    {
        using var buf = new NoteBuffer(2);
        buf.AddNote(60, Rational.Zero, Rational.Quarter);
        buf.AddNote(62, Rational.Quarter, Rational.Quarter);

        var result = PolyphonyAnalyzer.Analyze(buf);

        Assert.Empty(result.Intervals);
        Assert.Empty(result.Motions);
    }
}


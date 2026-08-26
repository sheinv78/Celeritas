// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.VoiceLeading;

namespace Celeritas.Tests;

/// <summary>
/// The counterpoint checks answer with a verdict, and a wrong verdict reads exactly like a right
/// one — there is no crash and no missing field to notice. Each test here is a passage whose
/// verdict was wrong for a reason that had nothing to do with the music: which voice was named
/// first, which slots happened to be empty, or whether some other voice moved in between.
/// </summary>
public class CounterpointVerdictTests
{
    private static NoteBuffer BufferOf(params (int Pitch, int OffsetEighths, int LengthEighths)[] notes)
    {
        var buffer = new NoteBuffer(notes.Length);
        foreach (var (pitch, offset, length) in notes)
            buffer.AddNote(pitch, new Rational(offset, 8), new Rational(length, 8));
        buffer.Sort();
        return buffer;
    }

    // ---------- a fifth is a fifth whichever voice is written first ----------

    [Fact]
    public void ParallelFifthsBetweenCrossedVoices_AreStillReported()
    {
        // Tenor above alto, a fifth apart, and no other pair in the chord forms a fifth or an
        // octave — so this is the only parallel there is to find. Measured from the voice named
        // first, the sounding fifth came out as 5, a fourth, and the parallel went unreported.
        var from = new Voicing(bass: 49, tenor: 67, alto: 60, soprano: 75);
        var to = new Voicing(bass: 51, tenor: 69, alto: 62, soprano: 77);

        var check = VoiceLeadingRules.Check(from, to);

        Assert.True(check.Violations.HasFlag(VoiceLeadingViolation.ParallelFifths),
            "a perfect fifth doubled in parallel went unreported because the voices were crossed");
    }

    [Fact]
    public void ParallelFourthsBetweenCrossedVoices_AreNotReportedAsFifths()
    {
        // The same crossing, a fourth apart, and again no other pair is a fifth or an octave.
        // Measured downwards a fourth reads as 7, so this was reported as parallel fifths — a
        // violation that was not in the music.
        var from = new Voicing(bass: 49, tenor: 65, alto: 60, soprano: 71);
        var to = new Voicing(bass: 51, tenor: 67, alto: 62, soprano: 73);

        var check = VoiceLeadingRules.Check(from, to);

        Assert.False(check.Violations.HasFlag(VoiceLeadingViolation.ParallelFifths),
            "parallel fourths were reported as parallel fifths");
    }

    [Fact]
    public void ParallelFifthsInUncrossedVoices_AreStillReported()
    {
        // The reading from the lower sounding pitch must not have turned the rule off.
        var from = new Voicing(bass: 48, tenor: 55, alto: 64, soprano: 72);
        var to = new Voicing(bass: 50, tenor: 57, alto: 66, soprano: 74);

        var check = VoiceLeadingRules.Check(from, to);

        Assert.True(check.Violations.HasFlag(VoiceLeadingViolation.ParallelFifths));
    }

    // ---------- spacing follows the parts, not the gaps between them ----------

    [Fact]
    public void ATenorAndBassAnOctaveAndAHalfApart_AreNotBadlySpaced()
    {
        // Two low voices and nothing above them. The spacing limit used to be picked from the
        // position in the list of voices that are present, so this pair was judged by the
        // soprano-alto octave rule and an ordinary duet was reported as badly spaced.
        using var buffer = BufferOf(
            (36, 0, 4), (55, 0, 4),
            (38, 4, 4), (57, 4, 4),
            (40, 8, 4), (59, 8, 4));

        var result = PolyphonyAnalyzer.CheckCounterpointRules(buffer);

        Assert.Equal(0, result.SpacingViolations);
    }

    [Fact]
    public void UpperVoicesMoreThanAnOctaveApart_AreStillReported()
    {
        // Three voices: the top two are seventeen semitones apart, which the rule does forbid.
        using var buffer = BufferOf(
            (36, 0, 4), (60, 0, 4), (77, 0, 4),
            (38, 4, 4), (62, 4, 4), (79, 4, 4));

        var result = PolyphonyAnalyzer.CheckCounterpointRules(buffer);

        Assert.True(result.SpacingViolations > 0, "a gap of more than an octave between upper voices went unreported");
    }

    // ---------- a dissonance that resolves is not unresolved ----------

    [Fact]
    public void ADissonanceThatResolves_IsNotReportedWhenAnotherVoiceMovesInBetween()
    {
        // Voices 1 and 2 hold a minor second across the beat and resolve on the next one. The
        // third voice moves twice in between, which adds a time point — and the resolution scan
        // took the pair's next recorded interval, which was the same dissonance still sounding.
        using var buffer = BufferOf(
            (72, 0, 4), (71, 0, 4), (48, 0, 2),
            (50, 2, 2),
            (72, 4, 4), (67, 4, 4), (52, 4, 4));

        var result = PolyphonyAnalyzer.CheckCounterpointRules(buffer);

        Assert.DoesNotContain(result.Violations, v => v.Type == "Unresolved Dissonance");
    }

    [Fact]
    public void ADissonanceThatNeverResolves_IsStillReported()
    {
        // The same shape, but the pair keeps its minor second to the end.
        using var buffer = BufferOf(
            (72, 0, 4), (71, 0, 4), (48, 0, 2),
            (50, 2, 2),
            (72, 4, 4), (71, 4, 4), (52, 4, 4));

        var result = PolyphonyAnalyzer.CheckCounterpointRules(buffer);

        Assert.Contains(result.Violations, v => v.Type == "Unresolved Dissonance");
    }

    // ---------- "in outer voices" means the outer voices ----------

    [Fact]
    public void HiddenPerfectIntervals_AreOnlyReportedBetweenTheOuterVoices()
    {
        // The top two voices move the same way by different amounts and land an octave apart —
        // a hidden octave between the soprano and the middle voice, with a bass underneath that
        // is nowhere near a perfect interval from either. The guard used to fire for any pair
        // that included the highest voice, and the text it attached called the pair the outer
        // voices whether it was or not.
        using var buffer = BufferOf(
            (48, 0, 4), (62, 0, 4), (72, 0, 4),
            (50, 4, 4), (64, 4, 4), (76, 4, 4));

        var result = PolyphonyAnalyzer.CheckCounterpointRules(buffer);
        var voiceCount = VoiceSeparator.Separate(buffer).Voices.Count;

        Assert.All(
            result.Violations.Where(v => v.Type == "Hidden Perfect Interval"),
            v => Assert.True(v.Voice1 == 0 && v.Voice2 == voiceCount - 1,
                $"voices {v.Voice1} and {v.Voice2} of {voiceCount} were described as the outer voices"));
    }

    // ---------- doubling is not a canon ----------

    [Fact]
    public void TwoVoicesDoublingOneMelodyInOctaves_AreNotACanon()
    {
        // The zero-delay guard only rejects the aligned match. A melody with a repeating figure
        // also matches itself at a shift, and at that shift the delay is not zero — so strict
        // octave doubling was reported as a canon.
        int[] melody = [60, 62, 64, 60, 60, 62, 64, 60];
        var notes = new List<(int, int, int)>();
        for (var i = 0; i < melody.Length; i++)
        {
            notes.Add((melody[i], i * 2, 2));
            notes.Add((melody[i] + 12, i * 2, 2));
        }

        using var buffer = BufferOf([.. notes]);

        Assert.False(PolyphonyAnalyzer.DetectImitation(buffer).HasImitation,
            "two voices playing one melody together were reported as a canon");
    }

    [Fact]
    public void AVoiceAnsweringAnotherLater_IsStillACanon()
    {
        // The same melody, but the second voice enters a bar later — which is what a canon is.
        int[] melody = [60, 62, 64, 60, 67, 65, 64, 62];
        var notes = new List<(int, int, int)>();
        for (var i = 0; i < melody.Length; i++)
        {
            notes.Add((melody[i], i * 2, 2));
            notes.Add((melody[i] + 12, (i * 2) + 8, 2));
        }

        using var buffer = BufferOf([.. notes]);

        Assert.True(PolyphonyAnalyzer.DetectImitation(buffer).HasImitation,
            "a voice answering another a bar later was not recognised as imitation");
    }
}

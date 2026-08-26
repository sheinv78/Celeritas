// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Accompaniment;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Three answers that came from how the data happened to be arranged rather than from the music:
/// which chord tones survived a cap depended on the order of an array, a single leap was counted
/// once per voice pair that happened to be sounding, and a lone voice's texture density was its
/// own voice count instead of the time-weighted figure the property documents.
/// </summary>
public class CountedOnceAndChosenByTheMusicTests
{
    private static ChordAssignment Assignment(int[] pitches) =>
        new(Rational.Zero, Rational.Half, ChordAnalyzer.Identify(pitches), pitches);

    private static int[] Voiced(int[] pitches, int maxChordTones) =>
        [.. AccompanimentGenerator
            .Generate([Assignment(pitches)], AccompanimentOptions.Default with { MaxChordTones = maxChordTones })
            .Select(n => n.Pitch)
            .Order()];

    // ---------- which tones survive the cap is a question about the chord ----------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheSameChordWrittenInADifferentOrder_IsVoicedTheSameWay(int maxChordTones)
    {
        // The cap kept whichever pitch classes came first in the array, so C7 written
        // [60,64,67,70] was voiced root-and-third and the same chord written [70,67,64,60] came
        // out seventh-and-fifth: re-ordering a caller's pitches changed the harmony.
        Assert.Equal(Voiced([60, 64, 67, 70], maxChordTones), Voiced([70, 67, 64, 60], maxChordTones));
        Assert.Equal(Voiced([60, 64, 67, 70], maxChordTones), Voiced([64, 70, 60, 67], maxChordTones));
    }

    [Fact]
    public void AnyPermutationOfAChordIsVoicedTheSameWay()
    {
        (from root in Gen.Int[0, 11]
         from maxChordTones in Gen.Int[1, 4]
         from stride in Gen.Int[1, 6]
         select (root, maxChordTones, stride)).Sample(t =>
        {
            int[] pitches = [60 + t.root, 64 + t.root, 67 + t.root, 70 + t.root];
            var permuted = pitches.OrderBy(p => p * t.stride % 7).ToArray();

            return Voiced(pitches, t.maxChordTones).SequenceEqual(Voiced(permuted, t.maxChordTones));
        }, iter: 500);
    }

    [Fact]
    public void ACappedVoicingKeepsWhatCarriesTheChord()
    {
        // Root, third and seventh before the fifth: the fifth is the note a player drops first.
        var three = Voiced([60, 64, 67, 70], 3).Select(PitchMath.Fold).Distinct().ToArray();

        Assert.Contains(0, three);      // the root
        Assert.Contains(4, three);      // the third
        Assert.Contains(10, three);     // the seventh
        Assert.DoesNotContain(7, three);
    }

    // ---------- a leap is something one voice does ----------

    [Fact]
    public void OneLeapIsReportedOnce_HoweverManyVoicesSoundBesideIt()
    {
        // Motions are recorded per voice PAIR, so a single leap in the top voice of four was
        // reported three times — once for each voice it happened to be paired with.
        using var buffer = new NoteBuffer(8);
        for (var voice = 0; voice < 4; voice++)
        {
            buffer.AddNote(72 - (voice * 12), Rational.Zero, Rational.Half);
            buffer.AddNote(voice == 0 ? 90 : 72 - (voice * 12), Rational.Half, Rational.Half);
        }

        buffer.Sort();

        var violations = PolyphonyAnalyzer.CheckCounterpointRules(buffer).Violations;

        Assert.Single(violations, v => v.Type == "Large Leap");
    }

    [Fact]
    public void TwoLeapsAtDifferentMoments_AreBothReported()
    {
        // Reporting a leap once per voice must not have become once per passage: the same voice
        // leaping twice is two leaps.
        using var buffer = new NoteBuffer(12);
        int[] top = [72, 90, 72];
        for (var step = 0; step < 3; step++)
        {
            buffer.AddNote(top[step], new Rational(step, 2), Rational.Half);
            buffer.AddNote(60, new Rational(step, 2), Rational.Half);
            buffer.AddNote(48, new Rational(step, 2), Rational.Half);
            buffer.AddNote(36, new Rational(step, 2), Rational.Half);
        }

        buffer.Sort();

        var leaps = PolyphonyAnalyzer.CheckCounterpointRules(buffer).Violations
            .Where(v => v.Type == "Large Leap")
            .ToArray();

        Assert.Equal(2, leaps.Length);
        Assert.Equal(2, leaps.Select(v => v.Time).Distinct().Count());
    }

    // ---------- texture density is a time-weighted average ----------

    [Fact]
    public void ALineSilentForHalfItsSpan_HasHalfTheTextureDensity()
    {
        // The single-voice path reported the voice COUNT, so a line with a bar's rest in it
        // came back at 1.0 — the same figure as a line that never stops.
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(62, new Rational(3, 4), Rational.Quarter);

        Assert.Equal(0.5f, PolyphonyAnalyzer.Analyze(buffer).TextureDensity, 3);
    }

    [Fact]
    public void ALineThatNeverStops_StillHasATextureDensityOfOne()
    {
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(62, Rational.Quarter, Rational.Quarter);

        Assert.Equal(1f, PolyphonyAnalyzer.Analyze(buffer).TextureDensity, 3);
    }
}

// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Syncopation is relative: a note is syncopated when it starts on a weaker metrical position
/// than the strongest position it sustains through. Measured against strong and medium beats
/// only, the library's own "Syncopated" pattern (eighth, quarter, eighth) scored 0.00 while the
/// same result said "featuring Syncopated pattern" — the quarter on the "and" of 1 sustains
/// through beat 2, which is only a weak beat, but still a stronger position than the one it
/// started on.
/// </summary>
public class RhythmAnalyzerSyncopationTests
{
    private static readonly Rational DottedQuarter = new(3, 8);

    /// <summary>Lays the durations end to end from the first barline, single pitch, and analyzes in <paramref name="meter"/>.</summary>
    private static RhythmAnalysisResult Analyze(TimeSignature meter, IEnumerable<Rational> durations)
    {
        var list = durations.ToList();
        using var buffer = new NoteBuffer(Math.Max(4, list.Count));
        var offset = Rational.Zero;
        foreach (var duration in list)
        {
            buffer.AddNote(60, offset, duration);
            offset += duration;
        }

        return RhythmAnalyzer.Analyze(buffer, meter);
    }

    private static IEnumerable<Rational> Repeat(int times, params Rational[] pattern) =>
        Enumerable.Repeat(pattern, times).SelectMany(p => p);

    private static Rational[] Pattern(string name) =>
        RhythmAnalyzer.CommonPatterns.Single(p => p.Name == name).Durations;

    [Fact]
    public void Analyze_TwoBarsOfTheSyncopatedPattern_SyncopatesEveryQuarter()
    {
        // Eighth, quarter, eighth four times over: the quarter starts on the "and" of the beat and
        // lasts through the next beat. Four of the twelve notes.
        var result = Analyze(TimeSignature.Common, Repeat(4, Pattern("Syncopated")));

        var syncopated = result.Events.Where(e => e.IsSyncopated).Select(e => e.Offset).ToArray();

        Assert.Equal([new(1, 8), new(5, 8), new(9, 8), new(13, 8)], syncopated);
        Assert.Equal(4, result.Statistics.SyncopatedNotes);
        Assert.Equal(1f / 3, result.Syncopation, 1e-6f);
        Assert.Contains("featuring Syncopated pattern", result.TextureDescription);
        Assert.Contains("highly syncopated (33%)", result.TextureDescription);
    }

    [Theory]
    [InlineData("Straight Quarters")]
    [InlineData("Straight Eighths")]
    [InlineData("Sixteenths")]
    public void Analyze_EvenNotes_HaveNoSyncopation(string pattern)
    {
        // Every note starts on a position at least as strong as anything it sustains through.
        var result = Analyze(TimeSignature.Common, Repeat(2, Pattern(pattern)));

        Assert.Equal(0, result.Statistics.SyncopatedNotes);
        Assert.Equal(0f, result.Syncopation);
    }

    [Fact]
    public void Analyze_Tresillo_SyncopatesTheMiddleNote()
    {
        // 3+3+2: the second dotted quarter starts on the "and" of 2 and lasts through beat 3.
        var result = Analyze(TimeSignature.Common, Repeat(2, Pattern("Tresillo")));

        var syncopated = result.Events.Where(e => e.IsSyncopated).Select(e => e.Offset).ToArray();

        Assert.Equal([new(3, 8), new(11, 8)], syncopated);
        Assert.True(result.Syncopation > 0f);
    }

    [Fact]
    public void Analyze_NoteEndingExactlyOnAStrongerPosition_IsNotSyncopated()
    {
        // The eighth on the "and" of 4 ends on the barline; it arrives at the downbeat, it does not
        // hold through it.
        var result = Analyze(TimeSignature.Common,
            [Rational.Quarter, Rational.Quarter, Rational.Quarter, Rational.Eighth, Rational.Eighth, Rational.Whole]);

        Assert.Equal(0, result.Statistics.SyncopatedNotes);
    }

    [Fact]
    public void Analyze_NoteOnTheDownbeat_IsNeverSyncopated()
    {
        // A dotted quarter on beat 1 sustains through beat 2, but nothing is stronger than where it began.
        var result = Analyze(TimeSignature.Common, Repeat(2, DottedQuarter, Rational.Eighth, Rational.Half));

        Assert.Equal(0, result.Statistics.SyncopatedNotes);
    }

    [Fact]
    public void Analyze_HalfNoteOnBeatTwoOfCommonTime_IsSyncopated()
    {
        // Beat 2 is weak, beat 3 is medium: the half note holds through a stronger beat.
        var result = Analyze(TimeSignature.Common, [Rational.Quarter, Rational.Half, Rational.Quarter]);

        var half = result.Events.Single(e => e.Duration == Rational.Half);

        Assert.True(half.IsSyncopated);
        Assert.Equal(1, result.Statistics.SyncopatedNotes);
    }

    [Fact]
    public void Analyze_NoteOnBeatThreeTiedThroughTheBarline_IsSyncopated()
    {
        // Beat 3 of 4/4 is medium; the dotted half that starts there holds through the next
        // downbeat. Nothing else in the phrase holds through a beat stronger than its own.
        var result = Analyze(TimeSignature.Common,
            [Rational.Quarter, Rational.Quarter, new Rational(3, 4), Rational.Quarter, Rational.Half]);

        var tied = result.Events.Single(e => e.Offset == Rational.Half);

        Assert.True(tied.IsSyncopated);
        Assert.Equal(1, result.Statistics.SyncopatedNotes);
    }
}

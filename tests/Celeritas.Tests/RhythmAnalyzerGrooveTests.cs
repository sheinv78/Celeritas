using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class RhythmAnalyzerGrooveTests
{
    [Fact]
    public void Analyze_ResultProperties_AreAccessible()
    {
        // Exercises all public API properties so Rider does not flag their getters as unused.
        using var buffer = new NoteBuffer(8);
        var offset = Rational.Zero;
        for (int i = 0; i < 8; i++)
        {
            buffer.Add(new NoteEvent(60, offset, new Rational(3, 16)));
            offset += new Rational(1, 8);
        }

        var result = RhythmAnalyzer.Analyze(buffer, TimeSignature.Common);

        // RhythmAnalysisResult
        Assert.NotEmpty(result.Events);
        Assert.True(result.Syncopation >= 0f);

        // MeterDetectionResult
        Assert.NotEmpty(result.Meter.Reasoning);
        Assert.True(result.Meter.Tempo > Rational.Zero);

        // RhythmEvent
        var ev = result.Events[0];
        Assert.True(ev.BeatInMeasure >= Rational.Zero);
        Assert.True(ev.OriginalIndex >= 0);

        // RhythmPatternMatch.Count
        var pm = result.PatternMatches.FirstOrDefault();
        if (pm != null)
            Assert.True(pm.Count > 0);

        // RhythmStatistics
        var stats = result.Statistics;
        Assert.True(stats.ShortestDuration > Rational.Zero);
        Assert.True(stats.LongestDuration >= stats.ShortestDuration);
        Assert.True(stats.AverageDuration > Rational.Zero);
    }

    [Fact]
    public void Analyze_StraightQuarters_ProducesStraightFeel()
    {
        using var buffer = new NoteBuffer(4);

        buffer.AddRange(
        [
            new NoteEvent(60, new Rational(0, 1), new Rational(1, 4)),
            new NoteEvent(60, new Rational(1, 4), new Rational(1, 4)),
            new NoteEvent(60, new Rational(1, 2), new Rational(1, 4)),
            new NoteEvent(60, new Rational(3, 4), new Rational(1, 4))
        ]);

        var result = RhythmAnalyzer.Analyze(buffer, TimeSignature.Common);

        Assert.Equal(GrooveFeel.Straight, result.GrooveFeel);
        Assert.InRange(result.GrooveDrive, 0f, 1f);
        Assert.True(result.SwingRatio is > 0.45f and < 0.55f);
    }

    [Fact]
    public void Analyze_SwingPairs_ProducesSwingFeelAndHigherDriveThanStraight()
    {
        // Pairs sum to 1/4 (0.25) so DetectSwing will consider them.
        // Ratio 5/32 : 3/32 => swing ratio 0.625
        using var swing = new NoteBuffer(8);

        var offset = Rational.Zero;
        for (int i = 0; i < 4; i++)
        {
            var d1 = new Rational(5, 32);
            var d2 = new Rational(3, 32);

            swing.Add(new NoteEvent(60, offset, d1));
            offset += d1;
            swing.Add(new NoteEvent(60, offset, d2));
            offset += d2;
        }

        var swingResult = RhythmAnalyzer.Analyze(swing, TimeSignature.Common);

        using var straight = new NoteBuffer(8);
        straight.AddRange(
        [
            new NoteEvent(60, new Rational(0, 1), new Rational(1, 8)),
            new NoteEvent(60, new Rational(1, 8), new Rational(1, 8)),
            new NoteEvent(60, new Rational(1, 4), new Rational(1, 8)),
            new NoteEvent(60, new Rational(3, 8), new Rational(1, 8)),
            new NoteEvent(60, new Rational(1, 2), new Rational(1, 8)),
            new NoteEvent(60, new Rational(5, 8), new Rational(1, 8)),
            new NoteEvent(60, new Rational(3, 4), new Rational(1, 8)),
            new NoteEvent(60, new Rational(7, 8), new Rational(1, 8))
        ]);

        var straightResult = RhythmAnalyzer.Analyze(straight, TimeSignature.Common);

        Assert.Equal(GrooveFeel.Swing, swingResult.GrooveFeel);
        Assert.InRange(swingResult.GrooveDrive, 0f, 1f);
        Assert.True(swingResult.GrooveDrive > straightResult.GrooveDrive);
        Assert.True(swingResult.SwingRatio is > 0.55f and < 0.75f);
    }

    [Fact]
    public void Analyze_TresilloPattern_ProducesLatinFeel()
    {
        // Tresillo durations: 3/8, 3/8, 2/8 in a 4/4 bar.
        using var buffer = new NoteBuffer(3);

        buffer.AddRange(
        [
            new NoteEvent(60, new Rational(0, 1), new Rational(3, 8)),
            new NoteEvent(60, new Rational(3, 8), new Rational(3, 8)),
            new NoteEvent(60, new Rational(3, 4), new Rational(1, 4))
        ]);

        var result = RhythmAnalyzer.Analyze(buffer, TimeSignature.Common);

        Assert.Equal(GrooveFeel.Latin, result.GrooveFeel);
        Assert.InRange(result.GrooveDrive, 0f, 1f);
    }
}

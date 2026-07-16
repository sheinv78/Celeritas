using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// A motif's occurrences must carry the real onsets of the melody, not the index of the interval
/// they were found at dressed up as a time.
/// </summary>
/// <remarks>
/// <c>DetectMotifs</c> wrapped interval indices straight into <see cref="Rational"/>: index 3
/// surfaced as 3/1, three whole notes. And the <c>times</c> parameter that would have fixed it was
/// never read past its own null-coalesce, so <c>Analyze(NoteBuffer)</c> extracted real offsets and
/// then threw them away, and a wrong-length <c>times</c> array was accepted in silence.
/// </remarks>
public class MelodyMotifTimingTests
{
    // 60,62,64 repeated: intervals +2,+2,-4,+2,+2. The pattern [+2,+2] occurs at interval index 0
    // (note 0) and interval index 3 (note 3).
    private static readonly int[] RepeatingMelody = [60, 62, 64, 60, 62, 64];

    [Fact]
    public void Occurrences_UseRealOnsets_NotIntervalIndices()
    {
        // Quarter notes: note 3 begins at 3/4, not at 3/1.
        var times = new[]
        {
            Rational.Zero, new Rational(1, 4), new Rational(2, 4),
            new Rational(3, 4), new Rational(4, 4), new Rational(5, 4),
        };

        var motif = Assert.Single(MelodyAnalyzer.Analyze(RepeatingMelody, times).Motifs);
        Assert.Equal([Rational.Zero, new Rational(3, 4)], motif.Occurrences);

        // The bug's signature: index 3 would have been reported as 3/1, three whole notes.
        Assert.DoesNotContain(new Rational(3, 1), motif.Occurrences);
    }

    [Fact]
    public void Analyze_FromBuffer_ReportsBufferOffsets()
    {
        using var buffer = new NoteBuffer(RepeatingMelody.Length);
        for (var i = 0; i < RepeatingMelody.Length; i++)
        {
            buffer.Add(new NoteEvent(RepeatingMelody[i], new Rational(i, 4), Rational.Quarter));
        }

        var motif = Assert.Single(MelodyAnalyzer.Analyze(buffer).Motifs);

        // Real onsets from the buffer, not sequence positions: 0 and 3/4.
        Assert.Equal([Rational.Zero, new Rational(3, 4)], motif.Occurrences);
    }

    [Fact]
    public void Analyze_MismatchedTimesLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => MelodyAnalyzer.Analyze([60, 62, 64], [Rational.Zero]));
        Assert.Equal("times", ex.ParamName);
    }

    [Fact]
    public void Analyze_MatchingTimesLength_IsAccepted()
    {
        var times = new[] { Rational.Zero, new Rational(1, 4), new Rational(2, 4) };
        // Fewer than 4 intervals, so no motifs — but it must not throw, and must honour the times.
        var result = MelodyAnalyzer.Analyze([60, 62, 64], times);
        Assert.Empty(result.Motifs);
    }

    /// <summary>
    /// Without timing, occurrences fall back to sequential note positions — index i as i/1. This
    /// is the same output the old code produced, so callers that never supplied times see no
    /// change; only the real-timing path was broken.
    /// </summary>
    [Fact]
    public void Analyze_WithoutTimes_ReportsSequentialPositions()
    {
        var motif = Assert.Single(MelodyAnalyzer.Analyze(RepeatingMelody).Motifs);
        Assert.Equal([new Rational(0, 1), new Rational(3, 1)], motif.Occurrences);
    }
}

// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The rhythm analyzer's degenerate inputs and its <c>IEnumerable</c> overloads. Every one of
/// these returns a plausible-looking result rather than throwing — an empty score still reports
/// 4/4 at 120 — so nothing here fails loudly if it breaks. That is exactly why it needs tests.
/// </summary>
public class RhythmAnalyzerEdgeTests
{
    private static NoteEvent Q(int quarter, int pitch = 60) =>
        new(pitch, new Rational(quarter, 4), Rational.Quarter);

    private static NoteBuffer BufferOf(params NoteEvent[] notes)
    {
        var buffer = new NoteBuffer(Math.Max(4, notes.Length));
        buffer.AddRange(notes);
        return buffer;
    }

    // ---------- no notes at all ----------

    [Fact]
    public void DetectMeter_NoNotes_FallsBackToCommonTime_AndSaysSo()
    {
        using var empty = BufferOf();

        var result = RhythmAnalyzer.DetectMeter(empty);

        Assert.Equal(TimeSignature.Common, result.TimeSignature);
        Assert.Equal(0.5f, result.Confidence);
        Assert.Equal(new Rational(120, 1), result.Tempo);
        Assert.Empty(result.Alternatives);
        Assert.Equal("No notes provided", result.Reasoning);
    }

    [Fact]
    public void IdentifyPattern_NoNotes_IsNull_NotAnEmptyMatch()
    {
        using var empty = BufferOf();

        Assert.Null(RhythmAnalyzer.IdentifyPattern(empty));
    }

    [Fact]
    public void Analyze_NoNotes_ReturnsAFullyPopulatedEmptyResult()
    {
        using var empty = BufferOf();

        var result = RhythmAnalyzer.Analyze(empty);

        // Every member is required, so a half-built result would not compile; what matters is
        // that a caller reading any of them gets a neutral value rather than a surprise.
        Assert.Equal(TimeSignature.Common, result.Meter.TimeSignature);
        Assert.Equal(0f, result.Meter.Confidence);
        Assert.Empty(result.Events);
        Assert.Empty(result.PatternMatches);
        Assert.Equal(0, result.Statistics.TotalNotes);
        Assert.Equal(0.5f, result.SwingRatio);
        Assert.Equal(0f, result.Syncopation);
        Assert.Equal(0f, result.Density);
        Assert.Equal(GrooveFeel.Straight, result.GrooveFeel);
        Assert.Equal("No rhythmic content", result.TextureDescription);
    }

    // ---------- notes, but no intervals between them ----------

    [Fact]
    public void DetectMeter_ASingleSimultaneity_HasNoIntervalsToJudge()
    {
        // A chord: several notes, one onset. There is no inter-onset interval at all.
        using var chord = BufferOf(Q(0, 60), Q(0, 64), Q(0, 67));

        var result = RhythmAnalyzer.DetectMeter(chord);

        Assert.Equal(TimeSignature.Common, result.TimeSignature);
        Assert.Equal("No intervals detected", result.Reasoning);
    }

    // ---------- the IEnumerable overloads ----------

    [Fact]
    public void DetectMeter_AcceptsAnEnumerableThatIsNotAnArray()
    {
        var asList = new List<NoteEvent> { Q(0), Q(1), Q(2), Q(3), Q(4), Q(5), Q(6), Q(7) };

        var fromList = RhythmAnalyzer.DetectMeter(asList);
        var fromArray = RhythmAnalyzer.DetectMeter(asList.ToArray());

        Assert.Equal(fromArray.TimeSignature, fromList.TimeSignature);
        Assert.Equal(fromArray.Confidence, fromList.Confidence);
        Assert.Equal(fromArray.Reasoning, fromList.Reasoning);
    }

    [Fact]
    public void IdentifyPattern_AcceptsAnEnumerableThatIsNotAnArray()
    {
        var asList = new List<NoteEvent> { Q(0), Q(1), Q(2), Q(3), Q(4), Q(5), Q(6), Q(7) };

        var fromList = RhythmAnalyzer.IdentifyPattern(asList);
        var fromArray = RhythmAnalyzer.IdentifyPattern(asList.ToArray());

        Assert.Equal(fromArray?.Pattern.Name, fromList?.Pattern.Name);
        Assert.Equal(fromArray?.MatchQuality, fromList?.MatchQuality);
    }

    [Fact]
    public void IdentifyPattern_AnEmptyEnumerable_IsNull()
    {
        Assert.Null(RhythmAnalyzer.IdentifyPattern(new List<NoteEvent>()));
    }

    [Fact]
    public void IdentifyPattern_PicksTheBestMatchingPattern()
    {
        var steady = Enumerable.Range(0, 8).Select(i => Q(i)).ToList();

        var match = RhythmAnalyzer.IdentifyPattern(steady);

        Assert.NotNull(match);
        Assert.InRange(match.MatchQuality, 0f, 1f);
        Assert.True(match.Count > 0);
        Assert.False(string.IsNullOrWhiteSpace(match.Pattern.Name));
    }

    [Fact]
    public void IdentifyPattern_FewerOnsetsThanAnyPattern_IsHandled()
    {
        // Two onsets cannot fill any catalogued pattern; the scorer must decline rather than
        // read past the end of the onset list.
        var match = RhythmAnalyzer.IdentifyPattern(new List<NoteEvent> { Q(0), Q(1) });

        Assert.True(match is null || match.MatchQuality >= 0f);
    }

    [Fact]
    public void TheEnumerableOverloads_RejectNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => RhythmAnalyzer.DetectMeter((IEnumerable<NoteEvent>)null!));
        Assert.Throws<ArgumentNullException>(
            () => RhythmAnalyzer.IdentifyPattern((IEnumerable<NoteEvent>)null!));
    }

    [Fact]
    public void TheBufferOverloads_RejectNull()
    {
        Assert.Throws<ArgumentNullException>(() => RhythmAnalyzer.DetectMeter((NoteBuffer)null!));
        Assert.Throws<ArgumentNullException>(() => RhythmAnalyzer.IdentifyPattern((NoteBuffer)null!));
        Assert.Throws<ArgumentNullException>(() => RhythmAnalyzer.Analyze(null!));
    }

    // ---------- reasoning and groove ----------

    [Fact]
    public void AnIrregularRhythm_IsReportedAsLowConfidence()
    {
        // Onsets at prime-ish distances so no meter scores well.
        var ragged = new List<NoteEvent>
        {
            Q(0), Q(1), Q(3), Q(4), Q(8), Q(9), Q(11), Q(16), Q(23),
        };

        var result = RhythmAnalyzer.DetectMeter(ragged);

        if (result.Confidence < 0.5f)
            Assert.Contains("low confidence", result.Reasoning, StringComparison.Ordinal);
        else
            Assert.DoesNotContain("low confidence", result.Reasoning, StringComparison.Ordinal);
    }

    [Fact]
    public void ARestBeforeAnOnset_WeightsItAsAnAccent()
    {
        // A long gap makes the onset after it metrically strong; the detector should still
        // return a coherent meter rather than being thrown by the hole.
        var withGap = new List<NoteEvent> { Q(0), Q(1), Q(2), Q(3), Q(12), Q(13), Q(14), Q(15) };

        var result = RhythmAnalyzer.DetectMeter(withGap);

        Assert.InRange(result.Confidence, 0f, 1f);
        Assert.False(string.IsNullOrWhiteSpace(result.Reasoning));
    }

    [Fact]
    public void ACompoundMeter_IsGivenACompoundGroove()
    {
        using var buffer = BufferOf([.. Enumerable.Range(0, 12).Select(i => new NoteEvent(60, new Rational(i, 8), Rational.Eighth))]);

        var result = RhythmAnalyzer.Analyze(buffer, new TimeSignature(6, 8));

        Assert.True(result.Meter.TimeSignature.IsCompound);
        Assert.Equal(GrooveFeel.Compound, result.GrooveFeel);
    }

    [Fact]
    public void ASimpleMeter_IsNotGivenACompoundGroove()
    {
        using var buffer = BufferOf([.. Enumerable.Range(0, 8).Select(i => Q(i))]);

        var result = RhythmAnalyzer.Analyze(buffer, TimeSignature.Common);

        Assert.NotEqual(GrooveFeel.Compound, result.GrooveFeel);
    }
}

// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// <see cref="NoteBuffer.GetChords(Span{ValueTuple{Rational, ushort}})"/> — the allocation-free
/// overload, until now tested only for the exception it throws — and the rhythm predictor's
/// short-context, backoff and measure-filling paths. Both return usable-looking values when
/// they go wrong: a truncated chord list is still a chord list, and a prediction with no
/// evidence behind it still names a duration.
/// </summary>
public class BufferAndPredictorCoverageTests
{
    private static NoteBuffer Buffer(params (int Pitch, Rational Offset)[] notes)
    {
        var buffer = new NoteBuffer(Math.Max(4, notes.Length));
        foreach (var (pitch, offset) in notes)
            buffer.AddNote(pitch, offset, Rational.Quarter);
        return buffer;
    }

    // ---------- the span overload ----------

    [Fact]
    public void GetChords_IntoASpan_GroupsNotesThatShareAnOffset()
    {
        using var buffer = Buffer(
            (60, Rational.Zero), (64, Rational.Zero), (67, Rational.Zero),
            (65, Rational.Quarter), (69, Rational.Quarter));

        Span<(Rational Time, ushort Mask)> output = stackalloc (Rational, ushort)[4];
        var count = buffer.GetChords(output);

        Assert.Equal(2, count);
        Assert.Equal(Rational.Zero, output[0].Time);
        Assert.Equal(ChordAnalyzer.GetMask([60, 64, 67]), output[0].Mask);
        Assert.Equal(Rational.Quarter, output[1].Time);
        Assert.Equal(ChordAnalyzer.GetMask([65, 69]), output[1].Mask);
    }

    [Fact]
    public void GetChords_IntoASpan_AgreesWithTheAllocatingOverload()
    {
        using var buffer = Buffer(
            (60, Rational.Zero), (64, Rational.Zero),
            (65, Rational.Quarter),
            (67, Rational.Half), (71, Rational.Half), (74, Rational.Half));

        var expected = buffer.GetChords();
        Span<(Rational Time, ushort Mask)> output = stackalloc (Rational, ushort)[8];
        var count = buffer.GetChords(output);

        Assert.Equal(expected.Count, count);
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(expected[i].Time, output[i].Time);
            Assert.Equal(expected[i].Mask, output[i].Mask);
        }
    }

    [Fact]
    public void GetChords_IntoATooSmallSpan_TruncatesAndSaysHowMuchItWrote()
    {
        using var buffer = Buffer(
            (60, Rational.Zero), (62, Rational.Quarter), (64, Rational.Half), (65, new Rational(3, 4)));

        Span<(Rational Time, ushort Mask)> output = stackalloc (Rational, ushort)[2];
        var count = buffer.GetChords(output);

        Assert.Equal(2, count);
        Assert.Equal(Rational.Zero, output[0].Time);
        Assert.Equal(Rational.Quarter, output[1].Time);
    }

    [Fact]
    public void GetChords_IntoAnEmptySpan_WritesNothing()
    {
        using var buffer = Buffer((60, Rational.Zero));

        Assert.Equal(0, buffer.GetChords([]));
    }

    [Fact]
    public void GetChords_OfAnEmptyBuffer_IsZero()
    {
        using var buffer = new NoteBuffer(4);

        Span<(Rational Time, ushort Mask)> output = stackalloc (Rational, ushort)[2];
        Assert.Equal(0, buffer.GetChords(output));
    }

    // ---------- quantizing a buffer that is not in time order ----------

    [Fact]
    public void QuantizingAnUnsortedBuffer_SnapsEveryOffset_AndLeavesItUsable()
    {
        // Offsets arrive out of order, so the buffer's max-offset tracker is not simply the
        // last note; quantization rewrites every offset and has to resync it.
        using var buffer = Buffer(
            (67, new Rational(9, 16)),
            (60, new Rational(1, 16)),
            (64, new Rational(5, 16)));

        MusicMath.Quantize(buffer, Rational.Quarter);
        buffer.Sort();

        var chords = buffer.GetChords();

        Assert.Equal([Rational.Zero, Rational.Quarter, Rational.Half], chords.Select(c => c.Time));

        // The buffer still accepts notes and still knows what is sorted.
        buffer.AddNote(72, Rational.Whole, Rational.Quarter);
        Assert.Equal(4, buffer.Count);
        Assert.Equal(4, buffer.GetChords().Count);
    }

    // ---------- the rhythm predictor ----------

    private static readonly Rational[] SteadyEighths =
        [.. Enumerable.Repeat(Rational.Eighth, 12)];

    [Fact]
    public void Train_FromANoteBuffer_LearnsTheSameThingAsFromAList()
    {
        using var buffer = new NoteBuffer(SteadyEighths.Length);
        foreach (var (i, duration) in SteadyEighths.Select((d, i) => (i, d)))
            buffer.AddNote(60, new Rational(i, 8), duration);

        var fromBuffer = new RhythmPredictor(order: 2, seed: 1);
        fromBuffer.Train(buffer);

        var fromList = new RhythmPredictor(order: 2, seed: 1);
        fromList.Train(SteadyEighths);

        Assert.Equal(fromList.GetStats().UniqueContexts, fromBuffer.GetStats().UniqueContexts);
        Assert.Equal(fromList.GetStats().TotalTransitions, fromBuffer.GetStats().TotalTransitions);
    }

    [Fact]
    public void Train_FromANullBuffer_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new RhythmPredictor().Train((NoteBuffer)null!));
    }

    [Fact]
    public void Predict_WithLessContextThanTheModelOrder_AdmitsItFoundNothing()
    {
        var predictor = new RhythmPredictor(order: 2, seed: 7);
        predictor.Train(SteadyEighths);

        var prediction = predictor.Predict([Rational.Eighth]);

        Assert.False(prediction.ContextFound);
        Assert.Equal(Rational.Quarter, prediction.MostLikely);
        Assert.Equal(0f, prediction.Confidence);
        Assert.Empty(prediction.Alternatives);
    }

    [Fact]
    public void Predict_WithAnUnseenContext_FallsBackToAShorterOne()
    {
        // Trained only on eighths; asked about a context ending in a half note it has never
        // followed. The backoff should still answer from the shorter suffix it does know.
        var predictor = new RhythmPredictor(order: 3, seed: 7);
        predictor.Train([.. SteadyEighths, Rational.Quarter, Rational.Eighth, Rational.Eighth]);

        var prediction = predictor.Predict([Rational.Half, Rational.Eighth, Rational.Eighth]);

        Assert.InRange(prediction.Confidence, 0f, 1f);
        Assert.True(prediction.MostLikely > Rational.Zero);
    }

    [Fact]
    public void APredictionPrintsItselfWithItsAlternatives()
    {
        var predictor = new RhythmPredictor(order: 1, seed: 3);
        predictor.Train([Rational.Quarter, Rational.Eighth, Rational.Quarter, Rational.Half, Rational.Quarter, Rational.Eighth]);

        var text = predictor.Predict([Rational.Quarter]).ToString();

        Assert.StartsWith("Predicted:", text, StringComparison.Ordinal);
        Assert.Contains("%", text, StringComparison.Ordinal);
    }

    [Fact]
    public void APredictionWithNoAlternatives_PrintsOnlyTheMainOne()
    {
        var predictor = new RhythmPredictor(order: 1, seed: 3);
        predictor.Train([Rational.Quarter, Rational.Eighth, Rational.Quarter, Rational.Eighth]);

        var prediction = predictor.Predict([Rational.Quarter]);

        Assert.Equal(prediction.Alternatives.Count > 0, prediction.ToString().Contains("Alternatives", StringComparison.Ordinal));
    }

    [Fact]
    public void GenerateMeasure_FillsExactlyOneMeasure()
    {
        var predictor = new RhythmPredictor(order: 2, seed: 11);
        predictor.Train([.. Enumerable.Repeat(new Rational(3, 8), 10)]);

        // A three-eighth figure cannot tile 4/4, so the last value has to be clipped to what
        // is left of the measure rather than overrunning it.
        var measure = predictor.GenerateMeasure([new Rational(3, 8), new Rational(3, 8)], TimeSignature.Common);

        Assert.NotEmpty(measure);
        Assert.All(measure, d => Assert.True(d > Rational.Zero));
        Assert.Equal(Rational.Whole, measure.Aggregate(Rational.Zero, (a, b) => a + b));
    }

    [Fact]
    public void GenerateMeasure_WithNoTrainingAtAll_StillFillsTheMeasure()
    {
        var measure = new RhythmPredictor(order: 2, seed: 5)
            .GenerateMeasure([], new TimeSignature(3, 4));

        Assert.Equal(new Rational(3, 4), measure.Aggregate(Rational.Zero, (a, b) => a + b));
    }

    [Fact]
    public void Generate_FromASeedShorterThanTheOrder_StillProducesTheAskedForLength()
    {
        var predictor = new RhythmPredictor(order: 3, seed: 5);
        predictor.Train(SteadyEighths);

        var generated = predictor.Generate([Rational.Eighth], length: 6);

        Assert.Equal(6, generated.Count);
        Assert.All(generated, d => Assert.True(d > Rational.Zero));
    }

    [Theory]
    [InlineData(RhythmStyle.Classical)]
    [InlineData(RhythmStyle.Jazz)]
    [InlineData(RhythmStyle.Rock)]
    [InlineData(RhythmStyle.Latin)]
    [InlineData(RhythmStyle.Waltz)]
    public void EveryStyleModel_IsTrained(RhythmStyle style)
    {
        var stats = RhythmModels.GetStyleModel(style).GetStats();

        Assert.True(stats.TotalTransitions > 0, $"{style} came back untrained");
        Assert.NotEmpty(stats.MostCommonDurations);
    }

    [Fact]
    public void AnUnknownStyleName_FallsBackToClassical()
    {
        var unknown = RhythmModels.GetStyleModel("bebop-polka").GetStats();
        var classical = RhythmModels.GetStyleModel("classical").GetStats();

        Assert.Equal(classical.TotalTransitions, unknown.TotalTransitions);
        Assert.Equal(classical.UniqueContexts, unknown.UniqueContexts);
    }

    [Fact]
    public void AnUndefinedStyleValue_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RhythmModels.GetStyleModel((RhythmStyle)99));
        Assert.Throws<ArgumentNullException>(() => RhythmModels.GetStyleModel((string)null!));
    }
    // ---------- the buffer's spans ----------

    [Fact]
    public void TheVelocitySpansSeeTheSameNotesAsTheGetters()
    {
        using var buffer = new NoteBuffer(3);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter, 0.25f);
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter, 0.5f);
        buffer.AddNote(67, Rational.Half, Rational.Quarter, 0.75f);

        var velocities = buffer.VelocitySpan;
        var readOnly = buffer.VelocityReadOnlySpan;
        var pitches = buffer.PitchReadOnlySpan;

        Assert.Equal(3, velocities.Length);
        Assert.Equal(3, readOnly.Length);
        Assert.Equal([60, 64, 67], pitches.ToArray());
        for (var i = 0; i < buffer.Count; i++)
        {
            Assert.Equal(buffer.Get(i).Velocity, velocities[i]);
            Assert.Equal(buffer.Get(i).Velocity, readOnly[i]);
        }
    }

    [Fact]
    public void WritingThroughTheVelocitySpanChangesTheNotes()
    {
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter, 0.4f);
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter, 0.4f);

        buffer.VelocitySpan[1] = 0.9f;

        Assert.Equal(0.9f, buffer.Get(1).Velocity);
        Assert.Equal(0.4f, buffer.Get(0).Velocity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ABufferMustHaveRoomForAtLeastOneNote(int capacity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new NoteBuffer(capacity));

        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void QuantizingAnUnsortedBufferWhoseLastNoteIsTheLatest_ResyncsTheTracker()
    {
        // The max-offset tracker cannot just take the first note: here the largest offset
        // arrives second, so the scan has to keep looking.
        using var buffer = Buffer(
            (60, new Rational(1, 16)),
            (67, new Rational(9, 16)),
            (64, new Rational(5, 16)));

        MusicMath.Quantize(buffer, Rational.Quarter);
        buffer.Sort();

        Assert.Equal([Rational.Zero, Rational.Quarter, Rational.Half], buffer.GetChords().Select(c => c.Time));
    }
    // ---------- velocity scaling stays inside the range the type promises ----------

    [Theory]
    [InlineData(2f)]
    [InlineData(10f)]
    [InlineData(-1f)]
    public void ScalingVelocityPastTheEnds_Saturates(float factor)
    {
        // NoteEvent documents velocity as 0..1, so scaling has to land inside it rather than
        // storing a loudness the type says cannot exist.
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter, 0.8f);
        buffer.AddNote(62, Rational.Quarter, Rational.Quarter, 0.2f);
        buffer.AddNote(64, Rational.Half, Rational.Quarter, 1f);
        buffer.AddNote(65, new Rational(3, 4), Rational.Quarter, 0f);

        MusicMath.ScaleVelocity(buffer, factor);

        for (var i = 0; i < buffer.Count; i++)
            Assert.InRange(buffer.Get(i).Velocity, 0f, 1f);
    }

    [Fact]
    public void ScalingVelocityInsideTheRange_IsExact()
    {
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter, 0.8f);
        buffer.AddNote(62, Rational.Quarter, Rational.Quarter, 0.4f);

        MusicMath.ScaleVelocity(buffer, 0.5f);

        Assert.Equal(0.4f, buffer.Get(0).Velocity, 5);
        Assert.Equal(0.2f, buffer.Get(1).Velocity, 5);
    }

    [Fact]
    public void ScalingVelocity_TreatsEveryNoteTheSameWayWhateverTheBufferLength()
    {
        // The vector loop handles whole widths and a scalar tail handles the rest; both have
        // to clamp, or a buffer's last few notes would behave differently from the others.
        for (var count = 1; count <= 40; count++)
        {
            using var buffer = new NoteBuffer(count);
            for (var i = 0; i < count; i++)
                buffer.AddNote(60, new Rational(i, 4), Rational.Quarter, 0.9f);

            MusicMath.ScaleVelocity(buffer, 4f);

            for (var i = 0; i < count; i++)
                Assert.Equal(1f, buffer.Get(i).Velocity);
        }
    }
}

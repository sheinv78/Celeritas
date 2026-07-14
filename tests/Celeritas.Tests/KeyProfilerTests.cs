// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class KeyProfilerTests
{
    [Fact]
    public void DetectFromPitches_EmptyNoteEvents_HasEmptyCorrelationsNotNull()
    {
        var result = KeyProfiler.DetectFromPitches(Array.Empty<NoteEvent>());

        Assert.NotNull(result.AllCorrelations);
        Assert.Empty(result.AllCorrelations);
        Assert.Equal(0f, result.Confidence);
        // Must not throw NRE
        Assert.Empty(result.TopKeys(3));
    }

    [Fact]
    public void DetectFromPitches_CMajorScale_DetectsCMajor()
    {
        int[] pitches = [60, 62, 64, 65, 67, 69, 71, 72];
        var result = KeyProfiler.DetectFromPitches(pitches);

        Assert.Equal(0, result.Key.Root);
        Assert.True(result.Key.IsMajor);
        Assert.NotEmpty(result.AllCorrelations);
        Assert.InRange(result.Confidence, 0f, 1f);
    }

    [Fact]
    public void AnalyzeModulations_NonPositiveStepSize_Throws()
    {
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KeyProfiler.AnalyzeModulations(buffer, new Rational(1, 1), Rational.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KeyProfiler.AnalyzeModulations(buffer, new Rational(1, 1), new Rational(-1, 4)));
    }

    [Fact]
    public void DetectFromBuffer_Confidence_IsClampedToUnitRange()
    {
        using var buffer = new NoteBuffer(4);
        // Highly ambiguous content (chromatic cluster)
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(61, Rational.Zero, Rational.Quarter);
        buffer.AddNote(62, Rational.Zero, Rational.Quarter);
        buffer.AddNote(63, Rational.Zero, Rational.Quarter);

        var result = KeyProfiler.DetectFromBuffer(buffer);

        Assert.InRange(result.Confidence, 0f, 1f);
    }
}

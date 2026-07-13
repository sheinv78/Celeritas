// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class RhythmAnalyzerStatsTests
{
    [Fact]
    public void Analyze_MixedDurations_AverageDurationIsExactMean()
    {
        // 1/4 + 1/2 => exact mean 3/8 (the old numerator-sum computed garbage
        // for mixed denominators).
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, new Rational(1, 4));
        buffer.AddNote(62, new Rational(1, 4), new Rational(1, 2));

        var result = RhythmAnalyzer.Analyze(buffer, TimeSignature.Common);

        Assert.Equal(new Rational(3, 8), result.Statistics.AverageDuration);
    }

    [Fact]
    public void Analyze_ThreeDurations_AverageDurationIsExactMean()
    {
        // 1/8 + 1/4 + 1/2 = 7/8 => mean 7/24
        using var buffer = new NoteBuffer(3);
        buffer.AddNote(60, Rational.Zero, new Rational(1, 8));
        buffer.AddNote(62, new Rational(1, 8), new Rational(1, 4));
        buffer.AddNote(64, new Rational(3, 8), new Rational(1, 2));

        var result = RhythmAnalyzer.Analyze(buffer, TimeSignature.Common);

        Assert.Equal(new Rational(7, 24), result.Statistics.AverageDuration);
    }

    [Theory]
    [InlineData(6, 8, true)]
    [InlineData(9, 8, true)]
    [InlineData(12, 8, true)]
    [InlineData(6, 4, true)]
    [InlineData(9, 4, true)]
    [InlineData(12, 4, true)]
    [InlineData(4, 4, false)]
    [InlineData(3, 4, false)]
    [InlineData(2, 2, false)]
    [InlineData(5, 4, false)]
    public void TimeSignature_IsCompound_RecognizesQuarterBasedCompoundMeters(int beats, int unit, bool expected)
    {
        var ts = new TimeSignature(beats, unit);
        Assert.Equal(expected, ts.IsCompound);
        Assert.Equal(!expected, ts.IsSimple);
    }

    [Fact]
    public void TimeSignature_CompoundSixFour_HasTwoStrongBeats()
    {
        Assert.Equal(2, new TimeSignature(6, 4).StrongBeats);
        Assert.Equal(2, TimeSignature.Compound6.StrongBeats);
    }

    [Fact]
    public void Analyze_ZeroSpanOnsets_DensityIsZeroWithoutDivideByZero()
    {
        // Two notes at the same offset with zero duration => zero measures spanned.
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Zero);
        buffer.AddNote(64, Rational.Zero, Rational.Zero);

        var result = RhythmAnalyzer.Analyze(buffer, TimeSignature.Common);

        Assert.Equal(0f, result.Density);
    }
}

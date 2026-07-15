using Celeritas.Core;
using Celeritas.Core.Orchestration;

namespace Celeritas.Tests;

/// <summary>
/// A degenerate instrument range must not be able to wedge the mapper.
/// </summary>
/// <remarks>
/// <c>ClampToRange</c> shifted by octaves in a <c>while</c> loop bounded only by the caller's
/// range. Against a MinPitch of <see cref="int.MaxValue"/> the climb ran to 2147483640, overflowed
/// unchecked, wrapped negative and started over: no exception, no allocation, just a wedged
/// thread — the one failure mode a caller cannot catch, log, or retry.
///
/// Both ends are covered here, and deliberately so. Validating <see cref="InstrumentRange"/> stops
/// that value being constructible, but <c>default</c> and <c>with { }</c> bypass a record struct's
/// constructor, so the arithmetic in <c>ClampToRange</c> is what actually guarantees a return.
/// </remarks>
public class OrchestrationRangeTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Fact]
    public void InstrumentRange_RejectsBoundsOutsideMidi()
    {
        Assert.Equal("MinPitch",
            Assert.Throws<ArgumentOutOfRangeException>(() => new InstrumentRange(int.MaxValue, int.MaxValue)).ParamName);
        Assert.Equal("MinPitch",
            Assert.Throws<ArgumentOutOfRangeException>(() => new InstrumentRange(-1, 60)).ParamName);
        Assert.Equal("MaxPitch",
            Assert.Throws<ArgumentOutOfRangeException>(() => new InstrumentRange(40, 128)).ParamName);
        Assert.Equal("MaxPitch",
            Assert.Throws<ArgumentOutOfRangeException>(() => new InstrumentRange(60, int.MinValue)).ParamName);
    }

    [Fact]
    public void InstrumentRange_RejectsInvertedRange()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new InstrumentRange(60, 40));
        Assert.Equal("MaxPitch", ex.ParamName);
    }

    [Fact]
    public void InstrumentRange_AcceptsRealRanges()
    {
        var bass = new InstrumentRange(40, 60);
        Assert.True(bass.Contains(48));
        Assert.False(bass.Contains(72));

        Assert.Equal(0, new InstrumentRange(0, 127).MinPitch);
        Assert.Equal(60, new InstrumentRange(60, 60).MaxPitch); // a single-pitch range is legal
    }

    /// <summary>
    /// The mapper must return for any range the type can still hold — including the ones
    /// <c>default</c> and <c>with</c> let through behind the constructor's back.
    /// </summary>
    [Theory]
    [MemberData(nameof(DegenerateRanges))]
    public async Task Map_AlwaysReturns(InstrumentRange range)
    {
        var options = new OrchestrationOptions(
            SplitPitch: 54,
            Bass: new OrchestrationPartDefinition(OrchestrationPartKind.Bass, "Bass", range),
            Harmony: OrchestrationOptions.Default.Harmony);

        // 41 is below SplitPitch, so it routes to Bass and meets `range`. The exact value matters
        // more than it looks: the old descending loop overshot MaxPitch and overflowed only when
        // `(pitch - MaxPitch)` was not a multiple of 12. Against int.MinValue, pitch 40 happens to
        // land exactly on the bound and terminate — after 178,956,974 iterations — so a test
        // written with 40 passes for the wrong reason and proves nothing. 41 does not divide.
        NoteEvent[] melody = [new NoteEvent(41, Rational.Zero, Rational.Quarter)];

        var result = await CompletesWithin(() => OrchestrationMapper.Map(melody, options),
            $"Map did not return for range {range}.");
        Assert.Single(result.Bass.Notes);
    }

    public static TheoryData<InstrumentRange> DegenerateRanges() =>
    [
        default,                                                    // (0, 0)
        default(InstrumentRange) with { MinPitch = int.MaxValue },  // the original repro: climbs, overflows
        default(InstrumentRange) with { MaxPitch = int.MinValue },  // descends, overflows
        default(InstrumentRange) with { MinPitch = 100 },           // inverted: 100..0
        new InstrumentRange(60, 61),                                // narrower than an octave
        new InstrumentRange(0, 127),
    ];

    /// <summary>
    /// An extreme pitch must not cost an octave-sized loop either. The engine produces these:
    /// <c>MusicMath.Transpose</c> documents that it does not clamp.
    /// </summary>
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(-1_000_000)]
    public async Task Map_AlwaysReturns_ForExtremePitches(int pitch)
    {
        NoteEvent[] melody = [new NoteEvent(pitch, Rational.Zero, Rational.Quarter)];

        await CompletesWithin(() => OrchestrationMapper.Map(melody, OrchestrationOptions.Default),
            $"Map did not return for pitch {pitch}.");
    }

    /// <summary>
    /// A wedged thread cannot be asserted against directly — the test would hang with it — so run
    /// the call off to the side and fail if it does not come back.
    /// </summary>
    private static async Task<OrchestrationResult> CompletesWithin(Func<OrchestrationResult> act, string because)
    {
        var task = Task.Run(act);
        var finished = await Task.WhenAny(task, Task.Delay(Patience));

        Assert.True(ReferenceEquals(finished, task), because);
        return await task;
    }

    [Fact]
    public void Map_KeepsPitchClassWhenAnOctaveFits()
    {
        var options = new OrchestrationOptions(
            SplitPitch: 54,
            Bass: new OrchestrationPartDefinition(OrchestrationPartKind.Bass, "Bass", new InstrumentRange(40, 60)),
            Harmony: OrchestrationOptions.Default.Harmony);

        // C1 (24) is below the range; the nearest C at or above 40 is C3 (48).
        NoteEvent[] melody = [new NoteEvent(24, Rational.Zero, Rational.Quarter)];

        var mapped = OrchestrationMapper.Map(melody, options).Bass.Notes;
        Assert.Equal(48, Assert.Single(mapped).Pitch);
    }
}

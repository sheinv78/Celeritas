using Celeritas.Core;
using Celeritas.Core.Orchestration;
using Xunit;

namespace Celeritas.Tests;

public sealed class OrchestrationMapperTests
{
    [Fact]
    public void Map_SplitsByPitchAndConstrainsToRanges()
    {
        var notes = new[]
        {
            // Very low C0 -> should be shifted up into bass range.
            new NoteEvent(12, Rational.Zero, Rational.Quarter, 0.9f),

            // Typical harmony notes.
            new NoteEvent(60, Rational.Zero, Rational.Quarter, 0.6f),
            new NoteEvent(64, Rational.Zero, Rational.Quarter, 0.6f),
            new NoteEvent(67, Rational.Zero, Rational.Quarter, 0.6f),

            // Very high note -> should be shifted down into harmony range.
            new NoteEvent(108, Rational.Zero, Rational.Quarter, 0.6f)
        };

        var options = OrchestrationOptions.Default with
        {
            SplitPitch = 54,
            Bass = new OrchestrationPartDefinition(OrchestrationPartKind.Bass, "Bass", new InstrumentRange(28, 60)),
            Harmony = new OrchestrationPartDefinition(OrchestrationPartKind.Harmony, "Harmony", new InstrumentRange(48, 84))
        };

        var result = OrchestrationMapper.Map(notes, options);

        Assert.Single(result.Bass.Notes);
        Assert.Equal(4, result.Harmony.Notes.Length);

        // Bass should be within range and preserve pitch class C.
        var bassPitch = result.Bass.Notes[0].Pitch;
        Assert.InRange(bassPitch, options.Bass.Range.MinPitch, options.Bass.Range.MaxPitch);
        Assert.Equal(0, bassPitch % 12);

        // High note should be pulled down into harmony range, preserving pitch class.
        var high = result.Harmony.Notes[^1].Pitch;
        Assert.InRange(high, options.Harmony.Range.MinPitch, options.Harmony.Range.MaxPitch);
        Assert.Equal(108 % 12, high % 12);
    }
}

using Celeritas.Core;
using Celeritas.Core.Orchestration;

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

    /// <summary>
    /// The slot in <see cref="OrchestrationOptions"/> decides which notes a part receives; the
    /// <see cref="OrchestrationPartKind"/> on its definition is the label the part carries out.
    /// Every pairing of the two kinds — the conventional one, the swapped one, and both slots
    /// claiming the same kind — maps the notes exactly as the default does, and each result part
    /// reports the definition it was given, kind included.
    /// </summary>
    [Theory]
    [InlineData(OrchestrationPartKind.Bass, OrchestrationPartKind.Harmony)]
    [InlineData(OrchestrationPartKind.Harmony, OrchestrationPartKind.Bass)]
    [InlineData(OrchestrationPartKind.Bass, OrchestrationPartKind.Bass)]
    [InlineData(OrchestrationPartKind.Harmony, OrchestrationPartKind.Harmony)]
    public void Map_TheSlotDecidesAndKindIsOnlyTheLabel(
        OrchestrationPartKind bassSlotKind, OrchestrationPartKind harmonySlotKind)
    {
        var notes = new[]
        {
            new NoteEvent(12, Rational.Zero, Rational.Quarter, 0.9f),            // C0: bass, shifted up
            new NoteEvent(40, Rational.Quarter, Rational.Quarter, 0.9f),         // E2: bass, in range
            new NoteEvent(53, Rational.Half, Rational.Quarter, 0.7f),            // F3: last pitch below the split
            new NoteEvent(54, Rational.Half, Rational.Quarter, 0.7f),            // F#3: the split itself is harmony
            new NoteEvent(67, new Rational(3, 4), Rational.Quarter, 0.6f),       // G4: harmony, in range
            new NoteEvent(108, new Rational(3, 4), Rational.Quarter, 0.6f)       // C8: harmony, shifted down
        };

        var conventional = OrchestrationOptions.Default;
        var relabelled = conventional with
        {
            Bass = conventional.Bass with { Kind = bassSlotKind },
            Harmony = conventional.Harmony with { Kind = harmonySlotKind }
        };

        var expected = OrchestrationMapper.Map(notes, conventional);
        var actual = OrchestrationMapper.Map(notes, relabelled);

        Assert.Equal(2, actual.Parts.Count());
        Assert.Equal(3, actual.Bass.Notes.Length);
        Assert.Equal(3, actual.Harmony.Notes.Length);
        Assert.Equal(Events(expected.Bass), Events(actual.Bass));
        Assert.Equal(Events(expected.Harmony), Events(actual.Harmony));

        Assert.Equal(relabelled.Bass, actual.Bass.Definition);
        Assert.Equal(relabelled.Harmony, actual.Harmony.Definition);
        Assert.Equal(bassSlotKind, actual.Bass.Definition.Kind);
        Assert.Equal(harmonySlotKind, actual.Harmony.Definition.Kind);

        static IEnumerable<(int Pitch, Rational Offset, Rational Duration, float Velocity)> Events(OrchestratedPart part)
            => part.Notes.Select(n => (n.Pitch, n.Offset, n.Duration, n.Velocity));
    }
}

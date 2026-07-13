// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class ModulationDetectorTests
{
    private static void AddTriad(List<NoteEvent> notes, int[] pitches, Rational offset, Rational duration)
    {
        foreach (var p in pitches)
            notes.Add(new NoteEvent(p, offset, duration));
    }

    // Triads (MIDI): C major, B major, F# major, E major
    private static readonly int[] CMaj = [60, 64, 67];
    private static readonly int[] BMaj = [59, 63, 66];
    private static readonly int[] FsMaj = [54, 58, 61];
    private static readonly int[] EMaj = [64, 68, 71];

    [Fact]
    public void Analyze_ShortPiece_StillReportsModulation()
    {
        // Only 6 chords: with the old fixed windowSize = 8 the loop never ran and
        // pieces with <= 8 chords reported nothing.
        var notes = new List<NoteEvent>();
        var spacing = new Rational(1, 2);
        int[][] chords = [CMaj, CMaj, CMaj, BMaj, FsMaj, FsMaj];
        for (var i = 0; i < chords.Length; i++)
            AddTriad(notes, chords[i], spacing * i, spacing);

        var result = ModulationDetector.Analyze(notes.ToArray(), new KeySignature("C", true));

        Assert.NotEmpty(result.Modulations);
        Assert.All(result.Modulations, m =>
            Assert.False(m.ToKey.Root == 0 && m.ToKey.IsMajor, "Detected key must differ from C major"));
    }

    [Fact]
    public void Analyze_StableTonicization_EmitsSingleEventNotOnePerIndex()
    {
        // A short foreign-key area whose stability holds across several indices
        // must produce ONE event, not a duplicate at every consecutive index.
        var notes = new List<NoteEvent>();
        var spacing = new Rational(1, 4); // tight spacing keeps the new-key area short
        int[][] chords = [CMaj, CMaj, CMaj, BMaj, FsMaj, EMaj, BMaj, FsMaj, EMaj, BMaj];
        for (var i = 0; i < chords.Length; i++)
            AddTriad(notes, chords[i], spacing * i, spacing);

        var result = ModulationDetector.Analyze(notes.ToArray(), new KeySignature("C", true));

        Assert.NotEmpty(result.Modulations);

        // No duplicate events for the same target key: consecutive-index re-fires
        // while stability holds must be coalesced into a single event.
        var perTargetKey = result.Modulations
            .GroupBy(m => (m.ToKey.Root, m.ToKey.IsMajor))
            .Select(g => g.Count());
        Assert.All(perTargetKey, count => Assert.Equal(1, count));
    }

    [Fact]
    public void Analyze_TrueModulation_UpdatesEndKey()
    {
        var notes = new List<NoteEvent>();
        var spacing = new Rational(1, 2); // long new-key area => true modulation
        int[][] chords = [CMaj, CMaj, CMaj, BMaj, FsMaj, EMaj, BMaj, FsMaj, EMaj, BMaj];
        for (var i = 0; i < chords.Length; i++)
            AddTriad(notes, chords[i], spacing * i, spacing);

        var startKey = new KeySignature("C", true);
        var result = ModulationDetector.Analyze(notes.ToArray(), startKey);

        Assert.NotEmpty(result.Modulations);
        Assert.NotEqual(startKey, result.EndKey);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsStartKey()
    {
        var startKey = new KeySignature("D", false);
        var result = ModulationDetector.Analyze(ReadOnlySpan<NoteEvent>.Empty, startKey);

        Assert.Empty(result.Modulations);
        Assert.Equal(startKey, result.EndKey);
    }
}

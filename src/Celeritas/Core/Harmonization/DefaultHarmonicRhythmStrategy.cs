// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Default strategy: one chord per beat (or per note if longer than a beat).
/// </summary>
public sealed class DefaultHarmonicRhythmStrategy : IHarmonicRhythmStrategy
{
    private readonly Rational _beatDuration;

    public DefaultHarmonicRhythmStrategy(Rational? beatDuration = null)
    {
        _beatDuration = beatDuration ?? Rational.Quarter;
    }

    public IReadOnlyList<MelodySlice> Segment(ReadOnlySpan<NoteEvent> melody)
    {
        if (melody.IsEmpty)
            return [];

        var slices = new List<MelodySlice>();

        // Find time range
        var minStart = melody[0].Offset;
        var maxEnd = melody[0].Offset + melody[0].Duration;
        foreach (var note in melody)
        {
            if (note.Offset < minStart) minStart = note.Offset;
            var end = note.Offset + note.Duration;
            if (end > maxEnd) maxEnd = end;
        }

        // Quantize to beat grid
        var beatStart = QuantizeDown(minStart, _beatDuration);
        var beatEnd = QuantizeUp(maxEnd, _beatDuration);

        // Create slices
        var current = beatStart;
        var beatIndex = 0;
        while (current < beatEnd)
        {
            var sliceEnd = current + _beatDuration;
            var pitches = CollectPitches(melody, current, sliceEnd);

            if (pitches.Length > 0)
            {
                // Strong beat = first beat or every other beat (simplified)
                var isStrong = beatIndex % 2 == 0;
                slices.Add(new MelodySlice(current, sliceEnd, pitches, isStrong));
            }

            current = sliceEnd;
            beatIndex++;
        }

        return slices;
    }

    private static int[] CollectPitches(ReadOnlySpan<NoteEvent> melody, Rational start, Rational end)
    {
        var pitches = new List<int>();
        foreach (var note in melody)
        {
            var noteEnd = note.Offset + note.Duration;
            // Note overlaps with slice
            if (note.Offset < end && noteEnd > start)
            {
                pitches.Add(note.Pitch);
            }
        }
        return [.. pitches];
    }

    private static Rational QuantizeDown(Rational value, Rational grid)
    {
        // Floor to grid
        var beats = value / grid;
        var wholeBeats = (long)Math.Floor((double)beats.Numerator / beats.Denominator);
        return new Rational(wholeBeats * grid.Numerator, grid.Denominator);
    }

    private static Rational QuantizeUp(Rational value, Rational grid)
    {
        // Ceiling to grid
        var beats = value / grid;
        var wholeBeats = (long)Math.Ceiling((double)beats.Numerator / beats.Denominator);
        return new Rational(wholeBeats * grid.Numerator, grid.Denominator);
    }
}

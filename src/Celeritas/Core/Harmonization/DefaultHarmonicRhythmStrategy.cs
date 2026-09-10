// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Default strategy: one chord per beat (or per note if longer than a beat).
/// </summary>
/// <param name="beatDuration">Beat length in whole-note units; defaults to a quarter note when <see langword="null"/>.</param>
public sealed class DefaultHarmonicRhythmStrategy(Rational? beatDuration = null) : IHarmonicRhythmStrategy
{
    private readonly Rational _beatDuration = beatDuration ?? Rational.Quarter;

    /// <summary>
    /// Segments the melody into beat-aligned slices, skipping beats with no sounding notes. A
    /// beat in which nothing new begins — only notes held over from the beat before, and the
    /// same ones — joins the slice before it, so a note longer than a beat gets one chord.
    /// </summary>
    /// <remarks>
    /// The class has always been documented as one chord per beat "or per note if longer than a
    /// beat", and the second half of that was never true: a held whole note came back as four
    /// quarter-beat slices, and the harmonizer put four chords under it — C, F, C, C under a
    /// single sustained C. Beats without a fresh onset are folded into the slice that started
    /// the note now, and the strong-beat flag stays with that slice's own beat.
    /// </remarks>
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
                // Nothing begins in this beat and the notes are the ones already sounding: the
                // beat belongs to the slice that started them.
                if (slices.Count > 0
                    && !AnyNoteStartsIn(melody, current, sliceEnd)
                    && slices[^1].End == current
                    && pitches.AsSpan().SequenceEqual(slices[^1].Pitches))
                {
                    slices[^1] = slices[^1] with { End = sliceEnd };
                }
                else
                {
                    // Strong beat = first beat or every other beat (simplified)
                    var isStrong = beatIndex % 2 == 0;
                    slices.Add(new MelodySlice(current, sliceEnd, pitches, isStrong));
                }
            }

            current = sliceEnd;
            beatIndex++;
        }

        return slices;
    }

    private static bool AnyNoteStartsIn(ReadOnlySpan<NoteEvent> melody, Rational start, Rational end)
    {
        foreach (var note in melody)
        {
            if (note.Offset >= start && note.Offset < end)
            {
                return true;
            }
        }

        return false;
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

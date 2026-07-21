// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Orchestration;

/// <summary>
/// Maps engine-native notes to simple orchestrated parts and constrains pitches to ranges.
/// </summary>
public static class OrchestrationMapper
{
    /// <exception cref="ArgumentNullException"><paramref name="notes"/> is <see langword="null"/>.</exception>
    public static OrchestrationResult Map(NoteEvent[] notes, OrchestrationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var opt = options ?? OrchestrationOptions.Default;
        if (notes.Length == 0)
        {
            return new OrchestrationResult
            {
                Bass = new OrchestratedPart { Definition = opt.Bass, Notes = [] },
                Harmony = new OrchestratedPart { Definition = opt.Harmony, Notes = [] }
            };
        }

        var bass = new List<NoteEvent>(notes.Length / 2);
        var harmony = new List<NoteEvent>(notes.Length);

        for (var i = 0; i < notes.Length; i++)
        {
            var n = notes[i];
            var isBass = n.Pitch < opt.SplitPitch;
            if (isBass)
            {
                bass.Add(ClampToRange(n, opt.Bass.Range));
            }
            else
            {
                harmony.Add(ClampToRange(n, opt.Harmony.Range));
            }
        }

        return new OrchestrationResult
        {
            Bass = new OrchestratedPart { Definition = opt.Bass, Notes = [.. bass] },
            Harmony = new OrchestratedPart { Definition = opt.Harmony, Notes = [.. harmony] }
        };
    }

    private static NoteEvent ClampToRange(NoteEvent note, InstrumentRange range)
    {
        // Shift by octaves while preserving pitch class — arithmetic rather than a loop.
        //
        // `while (pitch < range.MinPitch) pitch += 12;` was unbounded on caller input: against a
        // MinPitch of int.MaxValue the climb ran to 2147483640, then overflowed unchecked, wrapped
        // negative, and started again. No exception, no allocation — just a wedged thread. The
        // range is validated now, so that particular input cannot arrive, but `default(...)` and
        // `with { }` both bypass a record struct's constructor, so the arithmetic is what actually
        // guarantees this returns.
        //
        // long, because `range.MinPitch - pitch` overflows int for a pitch near int.MinValue, and
        // NoteEvent permits one: MusicMath.Transpose does not clamp, by documented design.
        long pitch = note.Pitch;
        long min = range.MinPitch;
        long max = range.MaxPitch;

        if (pitch < min)
            pitch += 12 * ((min - pitch + 11) / 12);
        if (pitch > max)
            pitch -= 12 * ((pitch - max + 11) / 12);

        // If still out of range (extremely narrow ranges), clamp. Gives up the pitch class, which
        // is the honest answer when no octave of it fits between Min and Max.
        if (pitch < min)
            pitch = min;
        if (pitch > max)
            pitch = max;

        if (pitch == note.Pitch)
            return note;

        return new NoteEvent((int)pitch, note.Offset, note.Duration, note.Velocity);
    }
}

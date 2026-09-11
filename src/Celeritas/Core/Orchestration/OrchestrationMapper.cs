// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Orchestration;

/// <summary>
/// Maps engine-native notes to simple orchestrated parts and constrains pitches to ranges.
/// </summary>
public static class OrchestrationMapper
{
    /// <summary>
    /// Splits <paramref name="notes"/> into a bass part and a harmony part at
    /// <see cref="OrchestrationOptions.SplitPitch"/> and shifts each note by octaves into its
    /// part's range. Rests are silence and are scored for neither part.
    /// </summary>
    /// <remarks>
    /// The slot decides, not the kind. A note below <see cref="OrchestrationOptions.SplitPitch"/>
    /// goes to <see cref="OrchestrationOptions.Bass"/> and every other note to
    /// <see cref="OrchestrationOptions.Harmony"/>, whatever
    /// <see cref="OrchestrationPartDefinition.Kind"/> either definition carries; the definitions
    /// are copied onto the result unchanged, so each <see cref="OrchestratedPart"/> reports the
    /// kind and name it was given. Swapping the two kinds moves no note. The mapper has always
    /// worked this way; it had not said so, and the kind read as though it were consulted.
    /// </remarks>
    /// <param name="notes">The notes to orchestrate; rests are skipped.</param>
    /// <param name="options">
    /// The split point and the two part definitions; <see cref="OrchestrationOptions.Default"/>
    /// when <see langword="null"/>.
    /// </param>
    /// <returns>The bass and harmony parts, each carrying the definition it was mapped with.</returns>
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

            // Silence is not scored for anyone. A rest is RestPitch (-1), which is below every
            // SplitPitch, so it was handed to the bass part and then octave-shifted into range:
            // a bar of rests came out as a bass line on B1, and orchestrating a passage added
            // notes to it that nobody wrote.
            if (Rests.IsRest(n.Pitch)) continue;

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

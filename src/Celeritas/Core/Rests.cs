// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Drops silence from note data before it is analysed or written out.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MusicNotation.Parse(string, bool)"/> reports a rest as a note event whose pitch is
/// <see cref="MusicNotation.RestPitch"/> (-1), so a buffer filled straight from parsed notation
/// carries silence alongside its notes. Every reading that folds a pitch into a pitch class turns
/// that -1 into 11 and hears a B nobody played: a C major triad reads as Cmaj7, a phrase in C
/// major reads as E minor, a half-bar of silence counts as an onset, and an exported file gains a
/// note at the bottom of the keyboard. The wrong answer is a plausible one, which is what makes
/// it worth catching here rather than at each reading in turn.
/// </para>
/// <para>
/// The test is <c>== RestPitch</c> rather than <c>&lt; 0</c> deliberately.
/// <see cref="MusicMath.Transpose(NoteBuffer, int)"/> documents that it does not clamp, so a
/// transposed pitch can be negative and is still a note that was played.
/// </para>
/// </remarks>
internal static class Rests
{
    /// <summary>True when <paramref name="pitch"/> marks silence rather than a note.</summary>
    public static bool IsRest(int pitch) => pitch == MusicNotation.RestPitch;

    /// <summary>True when any of <paramref name="notes"/> is a rest.</summary>
    public static bool AnyIn(ReadOnlySpan<NoteEvent> notes)
    {
        foreach (ref readonly var note in notes)
        {
            if (IsRest(note.Pitch)) return true;
        }

        return false;
    }

    /// <summary>True when any note in <paramref name="buffer"/> is a rest.</summary>
    public static bool AnyIn(NoteBuffer buffer)
    {
        for (var i = 0; i < buffer.Count; i++)
        {
            if (IsRest(buffer.PitchAt(i))) return true;
        }

        return false;
    }

    /// <summary>
    /// The notes of <paramref name="notes"/> that sound. Returns the input unchanged when it
    /// holds no rests, so the common case allocates nothing.
    /// </summary>
    public static ReadOnlySpan<NoteEvent> Without(ReadOnlySpan<NoteEvent> notes) =>
        AnyIn(notes) ? Filtered(notes) : notes;

    /// <summary>The sounding notes of <paramref name="notes"/>, as an array.</summary>
    public static NoteEvent[] ToArrayWithout(ReadOnlySpan<NoteEvent> notes) =>
        AnyIn(notes) ? Filtered(notes) : notes.ToArray();

    /// <summary>
    /// A copy of <paramref name="buffer"/> holding only the notes that sound, or the buffer
    /// itself when it holds no rests. Dispose the result only when it is not the input — use
    /// <see cref="AnyIn(NoteBuffer)"/> to tell, or prefer <see cref="Sounding"/>.
    /// </summary>
    public static NoteBuffer CopyWithout(NoteBuffer buffer)
    {
        var copy = new NoteBuffer(Math.Max(1, buffer.Count));
        for (var i = 0; i < buffer.Count; i++)
        {
            var note = buffer.Get(i);
            if (!IsRest(note.Pitch)) copy.Add(note);
        }

        return copy;
    }

    /// <summary>
    /// The sounding notes of <paramref name="buffer"/>, together with whether the result is a
    /// new buffer the caller owns. Written for <c>using</c>:
    /// <code>
    /// var (notes, owned) = Rests.Sounding(buffer);
    /// try { /* read notes */ } finally { if (owned) notes.Dispose(); }
    /// </code>
    /// </summary>
    public static (NoteBuffer Notes, bool Owned) Sounding(NoteBuffer buffer) =>
        AnyIn(buffer) ? (CopyWithout(buffer), true) : (buffer, false);

    /// <summary>
    /// The 12-bit pitch-class mask of the sounding pitches among <paramref name="pitches"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="ChordAnalyzer.GetMask(ReadOnlySpan{int})"/> documents that it folds any integer
    /// into a pitch class, which is what a caller doing pitch-class arithmetic wants; this is the
    /// reading for a span that came from note data, where -1 means silence.
    /// </remarks>
    public static ushort MaskOf(ReadOnlySpan<int> pitches)
    {
        uint mask = 0;
        foreach (var pitch in pitches)
        {
            if (IsRest(pitch)) continue;
            mask |= (uint)(1 << (((pitch % 12) + 12) % 12));
        }

        return (ushort)mask;
    }

    /// <summary>
    /// Copies the pitches of the notes in <paramref name="notes"/> that sound into
    /// <paramref name="destination"/>, and returns how many there were. Written for the callers
    /// that stack-allocate a pitch span the size of the input and then read <c>[..count]</c>.
    /// </summary>
    public static int SoundingInto(ReadOnlySpan<NoteEvent> notes, Span<int> destination)
    {
        var next = 0;
        foreach (ref readonly var note in notes)
        {
            if (!IsRest(note.Pitch)) destination[next++] = note.Pitch;
        }

        return next;
    }

    private static NoteEvent[] Filtered(ReadOnlySpan<NoteEvent> notes)
    {
        var kept = new List<NoteEvent>(notes.Length);
        foreach (ref readonly var note in notes)
        {
            if (!IsRest(note.Pitch)) kept.Add(note);
        }

        return [.. kept];
    }
}

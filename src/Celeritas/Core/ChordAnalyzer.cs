// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core;

public static unsafe class ChordAnalyzer
{
    // Precomputed lookup: (pitch % 12) -> bit mask
    private static readonly ushort[] PitchToBitLookup;

    static ChordAnalyzer()
    {
        // Covers MIDI range 0-127, but works for any int via & 0x7F.
        PitchToBitLookup = new ushort[128];
        for (var i = 0; i < 128; i++)
        {
            PitchToBitLookup[i] = (ushort)(1 << (i % 12));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ushort GetMask(ReadOnlySpan<int> pitches)
    {
        if (pitches.IsEmpty) return 0;

        // For small chords (typical 3-6 notes), a simple loop is faster.
        if (pitches.Length <= 8)
        {
            return GetMaskScalar(pitches);
        }

        // For larger arrays, use lookup + unrolling.
        return GetMaskLookup(pitches);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort GetMaskScalar(ReadOnlySpan<int> pitches)
    {
        uint mask = 0;
        foreach (var p in pitches)
        {
            // Fast modulo via bit operations for non-negative values.
            // (p & 0x7F) guarantees indexing into [0-127].
            mask |= PitchToBitLookup[p & 0x7F];
        }
        return (ushort)mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort GetMaskLookup(ReadOnlySpan<int> pitches)
    {
        uint mask = 0;
        var i = 0;
        var len = pitches.Length;

        // Unroll by 4
        var limit = len - 3;
        for (; i <= limit; i += 4)
        {
            mask |= PitchToBitLookup[pitches[i] & 0x7F];
            mask |= PitchToBitLookup[pitches[i + 1] & 0x7F];
            mask |= PitchToBitLookup[pitches[i + 2] & 0x7F];
            mask |= PitchToBitLookup[pitches[i + 3] & 0x7F];
        }

        // Remainder
        for (; i < len; i++)
        {
            mask |= PitchToBitLookup[pitches[i] & 0x7F];
        }

        return (ushort)mask;
    }

    // Safe version for NoteBuffer without requiring unsafe context
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetMask(NoteBuffer buffer) => GetMask(buffer.PitchSpan);

    // Unsafe version for extreme cases
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetMask(int* pitches, int count) =>
        GetMask(new ReadOnlySpan<int>(pitches, count));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChordInfo Identify(ReadOnlySpan<int> pitches)
    {
        var mask = GetMask(pitches);
        return ChordLibrary.GetChord(mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChordInfo Identify(NoteBuffer buffer) => Identify(buffer.PitchSpan);

    /// <summary>
    /// Identify chord from a human-readable notation string.
    /// Examples: "C4 E4 G4" -> C major, "D4 F4 A4 C5" -> Dm7
    /// </summary>
    public static ChordInfo Identify(string notation)
    {
        var notes = MusicNotation.Parse(notation);
        if (notes.Length == 0)
            return ChordLibrary.GetChord(0);

        Span<int> pitches = stackalloc int[notes.Length];
        for (var i = 0; i < notes.Length; i++)
            pitches[i] = notes[i].Pitch;

        return Identify(pitches);
    }

    /// <summary>
    /// Identify chord from note events.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChordInfo Identify(ReadOnlySpan<NoteEvent> notes)
    {
        if (notes.IsEmpty)
            return ChordLibrary.GetChord(0);

        Span<int> pitches = stackalloc int[notes.Length];
        for (var i = 0; i < notes.Length; i++)
            pitches[i] = notes[i].Pitch;

        return Identify(pitches);
    }
}

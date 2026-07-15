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
        return pitches.IsEmpty switch
        {
            true => 0,
            _ => pitches.Length switch
            {
                // For small chords (typical 3-6 notes), a simple loop is faster.
                <= 8 => GetMaskScalar(pitches),
                _ => GetMaskLookup(pitches)
            }
        };

        // For larger arrays, use lookup + unrolling.
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PitchClassIndex(int p)
    {
        // Fast path for the MIDI range; exact mod-12 for out-of-range values
        // ((p & 0x7F) is NOT congruent to p mod 12 for negatives or p >= 128).
        return (uint)p <= 127u ? p : ((p % 12) + 12) % 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort GetMaskScalar(ReadOnlySpan<int> pitches)
    {
        uint mask = 0;
        foreach (var p in pitches)
        {
            mask |= PitchToBitLookup[PitchClassIndex(p)];
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
        var limit = len - 4;
        for (; i <= limit; i += 4)
        {
            mask |= PitchToBitLookup[PitchClassIndex(pitches[i])];
            mask |= PitchToBitLookup[PitchClassIndex(pitches[i + 1])];
            mask |= PitchToBitLookup[PitchClassIndex(pitches[i + 2])];
            mask |= PitchToBitLookup[PitchClassIndex(pitches[i + 3])];
        }

        // Remainder
        for (; i < len; i++)
        {
            mask |= PitchToBitLookup[PitchClassIndex(pitches[i])];
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
        var info = ChordLibrary.GetChord(mask);

        // Sus2/Sus4/Quartal share one pitch-class set (rotations of {0,2,7}), so the mask
        // lookup always answers Sus2. Use the actual bass note to disambiguate:
        // bass == r+7 of the Sus2 root => Sus4 on the bass; bass == r+2 => Quartal on the bass.
        if (info.Quality == ChordQuality.Sus2 && !pitches.IsEmpty)
        {
            var bass = pitches[0];
            foreach (var p in pitches)
            {
                if (p < bass) bass = p;
            }

            var bassPc = ((bass % 12) + 12) % 12;
            if (bassPc == (info.RootPitchClass + 7) % 12)
                return new ChordInfo((byte)bassPc, ChordQuality.Sus4);
            if (bassPc == (info.RootPitchClass + 2) % 12)
                return new ChordInfo((byte)bassPc, ChordQuality.Quartal);
        }

        return info;
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

        // The note count here is driven by the caller's string content, so it is unbounded.
        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];
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

        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];
        for (var i = 0; i < notes.Length; i++)
            pitches[i] = notes[i].Pitch;

        return Identify(pitches);
    }
}

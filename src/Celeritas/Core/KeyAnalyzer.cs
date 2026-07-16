// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celeritas.Core;

/// <summary>
/// Functional harmony analyzer using bitwise operations for performance
/// </summary>
public static class KeyAnalyzer
{
    // Scale masks for quick degree identification
    public const ushort MajorScaleMask = 0b101010110101; // C D E F G A B = bits 0,2,4,5,7,9,11
    public const ushort MinorScaleMask = 0b010110101101; // C D Eb F G Ab Bb = bits 0,2,3,5,7,8,10

    private static readonly ushort[] MajorScaleMasksByRoot;
    private static readonly ushort[] MinorScaleMasksByRoot;

    static KeyAnalyzer()
    {
        MajorScaleMasksByRoot = new ushort[12];
        MinorScaleMasksByRoot = new ushort[12];

        for (var root = 0; root < 12; root++)
        {
            // Transposing a scale UP by `root` semitones moves bit k to bit (k+root) mod 12,
            // i.e. a LEFT rotation of the 12-bit mask.
            MajorScaleMasksByRoot[root] = RotateLeft(MajorScaleMask, root);
            MinorScaleMasksByRoot[root] = RotateLeft(MinorScaleMask, root);
        }
    }

    /// <summary>
    /// Pitch-class mask of the major or natural-minor scale rooted at <paramref name="root"/> (0=C…11=B).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetScaleMask(int root, bool isMajor)
    {
        var index = PitchMath.Fold(root);
        return isMajor ? MajorScaleMasksByRoot[index] : MinorScaleMasksByRoot[index];
    }

    /// <summary>
    /// Analyze chord in the context of a key signature
    /// Uses cyclic rotation (ROR) to find scale degree
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static RomanNumeralChord Analyze(ReadOnlySpan<int> pitches, KeySignature key)
    {
        if (pitches.IsEmpty)
            return RomanNumeralChord.Invalid;

        // First, identify the chord quality independently
        var chord = ChordAnalyzer.Identify(pitches);
        if (chord.Quality == ChordQuality.Unknown)
            return RomanNumeralChord.Invalid;

        // Get chord root pitch class
        var chordRoot = ChordLibrary.GetPitchClass(chord.Root);

        // Calculate interval from key root to chord root
        var interval = (chordRoot - key.Root + 12) % 12;

        // Map interval to scale degree and function
        return key.IsMajor
            ? AnalyzeInMajorKey(interval, chord.Quality)
            : AnalyzeInMinorKey(interval, chord.Quality);
    }

    /// <summary>
    /// Analyze chord in the context of a key signature (array overload)
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="pitches"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RomanNumeralChord Analyze(int[] pitches, KeySignature key)
    {
        // Guard before the span conversion, not after: new ReadOnlySpan<int>(null) is legal
        // and yields an *empty* span rather than throwing, so an unguarded null would reach
        // the empty-input branch and be answered as if the caller had passed no notes.
        ArgumentNullException.ThrowIfNull(pitches);
        return Analyze(new ReadOnlySpan<int>(pitches), key);
    }

    /// <summary>
    /// Analyze chord in the context of a key signature (NoteEvent array overload)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static RomanNumeralChord Analyze(ReadOnlySpan<NoteEvent> notes, KeySignature key)
    {
        if (notes.IsEmpty)
            return RomanNumeralChord.Invalid;

        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];
        for (var i = 0; i < notes.Length; i++)
            pitches[i] = notes[i].Pitch;

        return Analyze(pitches, key);
    }

    /// <summary>
    /// Analyze chord in the context of a key signature (NoteEvent array overload)
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="notes"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RomanNumeralChord Analyze(NoteEvent[] notes, KeySignature key)
    {
        // AsSpan() is null-safe and returns an empty span, so null would be answered
        // as RomanNumeralChord.Invalid rather than reported as the mistake it is.
        ArgumentNullException.ThrowIfNull(notes);
        return Analyze(notes.AsSpan(), key);
    }

    private static RomanNumeralChord AnalyzeInMajorKey(int interval, ChordQuality quality)
    {
        return interval switch
        {
            0 => new RomanNumeralChord(ScaleDegree.I, quality, HarmonicFunction.Tonic),      // I
            2 => new RomanNumeralChord(ScaleDegree.Ii, quality, HarmonicFunction.Subdominant), // ii
            4 => new RomanNumeralChord(ScaleDegree.Iii, quality, HarmonicFunction.Tonic),    // iii
            5 => new RomanNumeralChord(ScaleDegree.Iv, quality, HarmonicFunction.Subdominant), // IV
            7 => new RomanNumeralChord(ScaleDegree.V, quality, HarmonicFunction.Dominant),   // V
            9 => new RomanNumeralChord(ScaleDegree.Vi, quality, HarmonicFunction.Tonic),     // vi
            11 => new RomanNumeralChord(ScaleDegree.Vii, quality, HarmonicFunction.Dominant), // vii°
            _ => RomanNumeralChord.Invalid
        };
    }

    private static RomanNumeralChord AnalyzeInMinorKey(int interval, ChordQuality quality)
    {
        return interval switch
        {
            0 => new RomanNumeralChord(ScaleDegree.I, quality, HarmonicFunction.Tonic),      // i
            2 => new RomanNumeralChord(ScaleDegree.Ii, quality, HarmonicFunction.Subdominant), // ii°
            3 => new RomanNumeralChord(ScaleDegree.Iii, quality, HarmonicFunction.Tonic),    // III
            5 => new RomanNumeralChord(ScaleDegree.Iv, quality, HarmonicFunction.Subdominant), // iv
            7 => new RomanNumeralChord(ScaleDegree.V, quality, HarmonicFunction.Dominant),   // V (or v)
            8 => new RomanNumeralChord(ScaleDegree.Vi, quality, HarmonicFunction.Tonic),     // VI
            10 => new RomanNumeralChord(ScaleDegree.Vii, quality, HarmonicFunction.Dominant), // VII
            _ => RomanNumeralChord.Invalid
        };
    }

    /// <summary>
    /// Identify key signature from a collection of pitches using bitwise correlation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature IdentifyKey(ReadOnlySpan<int> pitches)
    {
        if (pitches.IsEmpty)
            return new KeySignature(0, true);

        var mask = ChordAnalyzer.GetMask(pitches);
        return IdentifyKey(mask);
    }

    /// <summary>
    /// Identify key signature from a pitch class mask
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static KeySignature IdentifyKey(ushort mask)
    {
        // Try all 12 rotations for major and minor
        var bestMatch = 0;
        var bestCount = 0;
        var bestIsMajor = true;

        for (var root = 0; root < 12; root++)
        {
            var majorMask = MajorScaleMasksByRoot[root];
            var minorMask = MinorScaleMasksByRoot[root];

            // Count matching bits (pitch classes in scale)
            var majorCount = PopCount((ushort)(mask & majorMask));
            var minorCount = PopCount((ushort)(mask & minorMask));

            if (majorCount > bestCount)
            {
                bestCount = majorCount;
                bestMatch = root;
                bestIsMajor = true;
            }

            if (minorCount > bestCount)
            {
                bestCount = minorCount;
                bestMatch = root;
                bestIsMajor = false;
            }
        }

        return new KeySignature((byte)bestMatch, bestIsMajor);
    }

    /// <summary>
    /// Identify key signature from a collection of pitches (array overload)
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="pitches"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature IdentifyKey(int[] pitches)
    {
        // Without this, null becomes an empty span and is reported as C major.
        ArgumentNullException.ThrowIfNull(pitches);
        return IdentifyKey(new ReadOnlySpan<int>(pitches));
    }

    /// <summary>
    /// Identify key signature from a human-readable notation string.
    /// Example: "C4 D4 E4 F4 G4 A4 B4" -> C major
    /// </summary>
    private static KeySignature IdentifyKey(string notation)
    {
        var notes = MusicNotation.Parse(notation);
        if (notes.Length == 0)
            return new KeySignature(0, true);

        // The note count here is driven by the caller's string content, so it is unbounded.
        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];
        for (var i = 0; i < notes.Length; i++)
            pitches[i] = notes[i].Pitch;

        return IdentifyKey(pitches);
    }

    /// <summary>
    /// Identify key signature from note events.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static KeySignature IdentifyKey(ReadOnlySpan<NoteEvent> notes)
    {
        if (notes.IsEmpty)
            return new KeySignature(0, true);

        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];
        for (var i = 0; i < notes.Length; i++)
            pitches[i] = notes[i].Pitch;

        return IdentifyKey(pitches);
    }

    /// <summary>
    /// Alias for IdentifyKey for more intuitive API.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="notation"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature DetectKey(string notation)
    {
        // null used to parse to zero notes and be answered as C major. The guard belongs on
        // this public entry point so the exception names `notation`, not Parse's own `input`.
        ArgumentNullException.ThrowIfNull(notation);
        return IdentifyKey(notation);
    }

    /// <summary>
    /// Alias for IdentifyKey for more intuitive API.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature DetectKey(ReadOnlySpan<NoteEvent> notes) => IdentifyKey(notes);

    /// <summary>
    /// Alias for IdentifyKey for more intuitive API.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature DetectKey(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return IdentifyKey(buffer.PitchSpan);
    }

    /// <summary>
    /// Cyclic right rotation (ROR) for 12-bit mask. Moves bit k to bit (k-shift) mod 12,
    /// i.e. transposes a pitch-class mask DOWN by <paramref name="shift"/> semitones.
    /// A negative <paramref name="shift"/> rotates the other way, so
    /// <c>RotateRight(v, -1) == RotateLeft(v, 1)</c>.
    /// To transpose a scale to a root, use <see cref="GetScaleMask"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort RotateRight(ushort value, int shift)
    {
        shift = NormalizeShift(shift);
        return (ushort)(((value >> shift) | (value << (12 - shift))) & 0xFFF);
    }

    /// <summary>
    /// Cyclic left rotation for 12-bit mask. Moves bit k to bit (k+shift) mod 12,
    /// i.e. transposes a pitch-class mask UP by <paramref name="shift"/> semitones.
    /// A negative <paramref name="shift"/> rotates the other way, so
    /// <c>RotateLeft(v, -1) == RotateRight(v, 1)</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort RotateLeft(ushort value, int shift)
    {
        shift = NormalizeShift(shift);
        return (ushort)(((value << shift) | (value >> (12 - shift))) & 0xFFF);
    }

    /// <summary>
    /// Fold an arbitrary semitone count into [0, 12), the way <see cref="GetScaleMask"/> folds a root.
    /// </summary>
    /// <remarks>
    /// A bare <c>shift %= 12</c> leaves a negative shift negative, and C# then masks the shift count
    /// of <c>&gt;&gt;</c>/<c>&lt;&lt;</c> to 5 bits rather than rejecting it — so both halves of the
    /// rotation shifted past the end of the mask and OR'd to zero. A caller asking to transpose down
    /// by one semitone silently got back an empty scale instead of a rotated one.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NormalizeShift(int shift) => ((shift % 12) + 12) % 12;

    /// <summary>
    /// Population count (number of set bits) - Hamming weight
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PopCount(ushort value)
    {
        return BitOperations.PopCount(value);
    }
}

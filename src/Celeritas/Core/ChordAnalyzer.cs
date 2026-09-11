// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core;

/// <summary>
/// Identifies chords from sets of pitches, and computes 12-bit pitch-class masks
/// (bit <c>n</c> set means pitch class <c>n</c> is present).
/// </summary>
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

    /// <summary>Computes the 12-bit pitch-class mask of the given MIDI pitches.</summary>
    /// <param name="pitches">MIDI pitches (any integers; reduced modulo 12).</param>
    /// <returns>A mask where bit <c>n</c> is set when pitch class <c>n</c> is present.</returns>
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
    /// <summary>
    /// Computes the 12-bit pitch-class mask of the notes in <paramref name="buffer"/> that sound.
    /// Rests carry no pitch class and are ignored.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetMask(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Rests.MaskOf(buffer.PitchSpan);
    }

    // Unsafe version for extreme cases
    /// <summary>Computes the 12-bit pitch-class mask from a raw pointer to <paramref name="count"/> MIDI pitches.</summary>
    /// <param name="pitches">Pointer to the pitch array.</param>
    /// <param name="count">Number of pitches to read.</param>
    /// <returns>A mask where bit <c>n</c> is set when pitch class <c>n</c> is present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetMask(int* pitches, int count) =>
        GetMask(new ReadOnlySpan<int>(pitches, count));

    /// <summary>Identifies the chord formed by the given MIDI pitches (lowest pitch treated as bass).</summary>
    /// <param name="pitches">MIDI pitches; the minimum is used as the bass, both for sus/quartal
    /// disambiguation and to root the fully symmetric augmented and diminished-seventh chords.</param>
    /// <returns>The identified chord's root and quality.</returns>
    /// <remarks>
    /// The root of a symmetric chord is therefore voicing-dependent: C-E-G# identifies as C
    /// augmented, E-G#-C as E augmented.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChordInfo Identify(ReadOnlySpan<int> pitches)
    {
        var mask = GetMask(pitches);
        var info = ChordLibrary.GetChord(mask);

        // Qualities whose pitch-class set is shared by several rotations can only get one
        // answer from the mask lookup (the lowest registered root). Use the actual bass
        // note to disambiguate.
        if (!pitches.IsEmpty &&
            info.Quality is ChordQuality.Sus2 or ChordQuality.Augmented or ChordQuality.Diminished7
                or ChordQuality.Dominant7Flat5 or ChordQuality.Minor7 or ChordQuality.HalfDim7)
        {
            var bass = pitches[0];
            foreach (var p in pitches)
            {
                if (p < bass) bass = p;
            }

            var bassPc = PitchMath.Fold(bass);

            switch (info.Quality)
            {
                // Sus2/Sus4/Quartal share one pitch-class set (rotations of {0,2,7}), so the
                // mask lookup always answers Sus2. bass == r+7 of the Sus2 root => Sus4 on
                // the bass; bass == r+2 => Quartal on the bass.
                case ChordQuality.Sus2:
                    if (bassPc == (info.RootPitchClass + 7) % 12)
                        return new ChordInfo((byte)bassPc, ChordQuality.Sus4);
                    if (bassPc == (info.RootPitchClass + 2) % 12)
                        return new ChordInfo((byte)bassPc, ChordQuality.Quartal);
                    break;

                // Augmented ({0,4,8}) and dim7 ({0,3,6,9}) are fully symmetric: every chord
                // tone is a valid root and all rotations share one mask, so the lookup always
                // answers the lowest registered root. Prefer the rotation rooted on the bass.
                case ChordQuality.Augmented:
                case ChordQuality.Diminished7:
                    if (bassPc != info.RootPitchClass)
                        return new ChordInfo((byte)bassPc, info.Quality);
                    break;

                // A 7b5 ({0,4,6,10}) maps onto itself a tritone away, so the set has two
                // equally valid roots sharing one mask, and the lookup can only answer the
                // lower-numbered one. That is a fact about pitch-class numbering rather than
                // about the music: F#7b5 came back rooted on C, and the same chord transposed
                // reported a root that was not the transposed root.
                //
                // Take the candidate the bass sits nearest above — the reading in which the
                // bass is the most root-like member of the chord (root, then third, then
                // flat fifth, then seventh). That choice is made from the music, so it moves
                // with it.
                case ChordQuality.Dominant7Flat5:
                    {
                        var partner = (byte)((info.RootPitchClass + 6) % 12);
                        if (PitchMath.Fold(bassPc - partner) < PitchMath.Fold(bassPc - info.RootPitchClass))
                            return new ChordInfo(partner, info.Quality);
                        break;
                    }

                // A sixth chord and the seventh chord a minor third below it are the same four
                // pitch classes — {C,E,G,A} is both C6 and Am7, {C,Eb,G,A} both Cm6 and Am7b5 —
                // so the mask lookup can only ever answer the seventh, and did: a lead sheet
                // opening on C6 in C major was reported as opening on vi7, and Cm6, the melodic
                // minor tonic, came back as a half-diminished chord. The bass decides, exactly
                // as it does for sus and dim7 above: with the sixth-chord root in the bass this
                // is a sixth chord, and with the seventh's root in the bass it is a seventh.
                // Any other bass is an inversion of both and keeps the answer it had.
                case ChordQuality.Minor7:
                case ChordQuality.HalfDim7:
                    if (bassPc == (info.RootPitchClass + 3) % 12)
                    {
                        return new ChordInfo(
                            (byte)bassPc,
                            info.Quality == ChordQuality.Minor7
                                ? ChordQuality.Major6
                                : ChordQuality.Minor6);
                    }

                    break;
            }
        }

        return info;
    }

    /// <summary>
    /// Identifies the chord <paramref name="pitches"/> spell when their root is already known —
    /// from a chord symbol that named it. The answer is rooted on
    /// <paramref name="rootPitchClass"/> whatever the bass, and is
    /// <see cref="ChordQuality.Unknown"/> <em>on that root</em> when no template has those
    /// intervals above it, so a caller holding a ninth chord keeps its root.
    /// </summary>
    /// <remarks>
    /// <see cref="Identify(ReadOnlySpan{int})"/> reads the bass to root what it hears, which is
    /// the right question for notes and the wrong one for a symbol: "Am7/C" is A minor seventh
    /// in first inversion and was identified as C6, and "G9", which no template covers, as an
    /// Unknown chord rooted on C — so a ii7-V9-I was read in the key of its ii chord in eleven
    /// of twelve transpositions.
    /// </remarks>
    internal static ChordInfo Identify(ReadOnlySpan<int> pitches, int rootPitchClass)
    {
        var root = (byte)PitchMath.Fold(rootPitchClass);
        ChordLibrary.TryGetQuality(GetMask(pitches), root, out var quality);
        return new ChordInfo(root, quality);
    }

    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChordInfo Identify(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return IdentifySounding(buffer.PitchSpan);
    }

    /// <summary>
    /// Identifies the chord spelled by the pitches that sound, ignoring rests. Every overload
    /// that takes note data comes through here: a rest is <see cref="MusicNotation.RestPitch"/>
    /// (-1), and folding that gives pitch class 11, so a C major triad written with a rest after
    /// it identified as Cmaj7 — and the rest, being the lowest "pitch", was read as the bass.
    /// </summary>
    private static ChordInfo IdentifySounding(ReadOnlySpan<int> pitches)
    {
        var rests = 0;
        foreach (var pitch in pitches)
        {
            if (Rests.IsRest(pitch)) rests++;
        }

        if (rests == 0)
            return Identify(pitches);

        var sounding = pitches.Length - rests;
        if (sounding == 0)
            return ChordLibrary.GetChord(0);

        Span<int> kept = sounding <= StackAlloc.MaxInts ? stackalloc int[sounding] : new int[sounding];
        var next = 0;
        foreach (var pitch in pitches)
        {
            if (!Rests.IsRest(pitch)) kept[next++] = pitch;
        }

        return Identify(kept);
    }

    /// <summary>
    /// Identify chord from a human-readable notation string.
    /// Examples: "C4 E4 G4" -> C major, "D4 F4 A4 C5" -> Dm7
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="notation"/> is <see langword="null"/>.</exception>
    public static ChordInfo Identify(string notation)
    {
        // null used to parse to zero notes and be answered as the empty-mask chord, which
        // prints as "C Unknown" — indistinguishable from a legitimately empty input.
        ArgumentNullException.ThrowIfNull(notation);

        var notes = MusicNotation.Parse(notation);
        if (notes.Length == 0)
            return ChordLibrary.GetChord(0);

        // The note count here is driven by the caller's string content, so it is unbounded.
        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];
        for (var i = 0; i < notes.Length; i++)
            pitches[i] = notes[i].Pitch;

        return IdentifySounding(pitches);
    }

    /// <summary>
    /// Identify chord from note events. Rests are silence, not chord tones, and are ignored.
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

        return IdentifySounding(pitches);
    }
}

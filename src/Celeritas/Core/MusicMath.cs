// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;
using System.Runtime.CompilerServices;
using Celeritas.Core.Simd;

namespace Celeritas.Core;

/// <summary>
/// Bulk pitch/velocity/timing operations over a <c>NoteBuffer</c>, plus MIDI
/// note-name conversions. Timing is in whole-note units.
/// </summary>
public static unsafe class MusicMath
{
    private static readonly IPitchTransformer PitchTransposeImpl = PitchTransformerFactory.Best;

    /// <summary>
    /// Convert MIDI pitch to note name (e.g., 60 -> "C4").
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string MidiToNoteName(int midiPitch) => MusicNotation.ToNotation(midiPitch);

    /// <summary>
    /// Convert note name to MIDI pitch (e.g., "C4" -> 60).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="noteName"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NoteNameToMidi(string noteName)
    {
        // ParseNote throws too, but blames its own parameter — a caller of this method
        // would be told to go look for a "notation" argument it never passed.
        ArgumentNullException.ThrowIfNull(noteName);
        return MusicNotation.ParseNote(noteName);
    }

    /// <summary>
    /// Adds <paramref name="semitones"/> to every pitch. Results are NOT clamped to the MIDI
    /// 0-127 range; callers that need valid MIDI pitches must clamp afterwards.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Transpose(NoteBuffer buffer, int semitones)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.ThrowIfDisposed();
        // Single virtual call per operation (not per iteration);
        // inside - fully vectorized loop.
        PitchTransposeImpl.Transpose(buffer.PitchPtr, buffer.Count, semitones);
        GC.KeepAlive(buffer); // the raw-pointer loop must not outlive the buffer's finalizer
    }

    /// <summary>
    /// SIMD scaling of velocity, using a portable <see cref="Vector{T}"/> loop that the JIT
    /// widens to the platform's widest vector unit.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static void ScaleVelocity(NoteBuffer buffer, float factor)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.ThrowIfDisposed();

        var velocities = buffer.VelocityPtr;
        var count = buffer.Count;
        var i = 0;

        if (Vector.IsHardwareAccelerated && count >= Vector<float>.Count)
        {
            var vFactor = new Vector<float>(factor);
            var width = Vector<float>.Count;
            ref var start = ref Unsafe.AsRef<float>(velocities);
            for (; i <= count - width; i += width)
            {
                (Vector.LoadUnsafe(ref start, (nuint)i) * vFactor).StoreUnsafe(ref start, (nuint)i);
            }
        }

        for (; i < count; i++)
            velocities[i] *= factor;

        GC.KeepAlive(buffer);
    }

    /// <summary>
    /// Quantize note start times to a grid (round to nearest grid step, half-way cases round up).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static void Quantize(NoteBuffer buffer, Rational grid)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.ThrowIfDisposed();
        if (grid.Numerator <= 0)
            throw new ArgumentOutOfRangeException(nameof(grid), grid, "Quantization grid must be positive");

        var count = buffer.Count;
        if (count == 0) return;

        var offsetsNum = buffer.OffsetsNumPtr;
        var offsetsDen = buffer.OffsetsDenPtr;

        var gNum = grid.Numerator;
        var gDen = grid.Denominator;

        // When the grid and an offset all fit in Int32, every intermediate below is bounded by
        // ~2^62 and stays exact in Int64; only near-Int64 magnitudes need the 128-bit path.
        // gNum > 0 (checked above) and gDen > 0 always.
        var gridFitsInt32 = gNum <= int.MaxValue && gDen <= int.MaxValue;

        for (var i = 0; i < count; i++)
        {
            var offNum = offsetsNum[i];
            var offDen = offsetsDen[i]; // always > 0

            // offset / grid = (offNum * gDen) / (offDen * gNum). The divisor is positive, so
            // floor division rounds half-up correctly for negative offsets too.
            if (gridFitsInt32 && offNum is >= int.MinValue and <= int.MaxValue && offDen <= int.MaxValue)
            {
                var valNum = offNum * gDen;
                var valDen = offDen * gNum;
                var shifted = valNum + (valDen >> 1);
                var rounded = shifted >= 0
                    ? shifted / valDen
                    : -((-shifted + valDen - 1) / valDen);
                offsetsNum[i] = rounded * gNum;
                offsetsDen[i] = gDen;
                continue;
            }

            // Exact 128-bit fallback for pathological magnitudes.
            var valNum128 = (Int128)offNum * gDen;
            var valDen128 = (Int128)offDen * gNum;
            var shifted128 = valNum128 + (valDen128 >> 1);
            var rounded128 = shifted128 >= 0
                ? shifted128 / valDen128
                : -((-shifted128 + valDen128 - 1) / valDen128);
            offsetsNum[i] = checked((long)(rounded128 * gNum));
            offsetsDen[i] = gDen;
        }

        GC.KeepAlive(buffer);
    }
}

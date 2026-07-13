// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;
using System.Runtime.CompilerServices;
using Celeritas.Core.Simd;

namespace Celeritas.Core;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NoteNameToMidi(string noteName) => MusicNotation.ParseNote(noteName);

    /// <summary>
    /// Adds <paramref name="semitones"/> to every pitch. Results are NOT clamped to the MIDI
    /// 0-127 range; callers that need valid MIDI pitches must clamp afterwards.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Transpose(NoteBuffer buffer, int semitones)
    {
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
    public static void ScaleVelocity(NoteBuffer buffer, float factor)
    {
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
    public static void Quantize(NoteBuffer buffer, Rational grid)
    {
        buffer.ThrowIfDisposed();
        if (grid.Numerator <= 0)
            throw new ArgumentOutOfRangeException(nameof(grid), grid, "Quantization grid must be positive");

        var count = buffer.Count;
        if (count == 0) return;

        var offsetsNum = buffer.OffsetsNumPtr;
        var offsetsDen = buffer.OffsetsDenPtr;

        var gNum = grid.Numerator;
        var gDen = grid.Denominator;

        for (var i = 0; i < count; i++)
        {
            // offset / grid = (num * gDen) / (den * gNum); Int128 makes the cross-products exact.
            // Both den and gNum are positive, so valDen > 0 and floor division rounds half-up correctly
            // for negative offsets too.
            var valNum = (Int128)offsetsNum[i] * gDen;
            var valDen = (Int128)offsetsDen[i] * gNum;
            var shifted = valNum + (valDen >> 1);
            var rounded = shifted >= 0
                ? shifted / valDen
                : -((-shifted + valDen - 1) / valDen);
            offsetsNum[i] = checked((long)(rounded * gNum));
            offsetsDen[i] = gDen;
        }

        GC.KeepAlive(buffer);
    }
}

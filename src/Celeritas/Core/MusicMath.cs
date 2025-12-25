// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Transpose(NoteBuffer buffer, int semitones)
    {
        // Single virtual call per operation (not per iteration);
        // inside - fully vectorized loop.
        PitchTransposeImpl.Transpose(buffer.PitchPtr, buffer.Count, semitones);
    }

    /// <summary>
    /// SIMD scaling of velocity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ScaleVelocity(NoteBuffer buffer, float factor)
    {
        var velocities = buffer.VelocityPtr;
        var count = buffer.Count;

        if (Avx512F.IsSupported && count >= 16)
        {
            var vFactor = Vector512.Create(factor);
            var i = 0;
            for (; i <= count - 16; i += 16)
            {
                var v = Avx512F.LoadVector512(velocities + i);
                v = Avx512F.Multiply(v, vFactor);
                Avx512F.Store(velocities + i, v);
            }
            for (; i < count; i++)
                velocities[i] *= factor;
        }
        else if (Avx.IsSupported && count >= 8)
        {
            var vFactor = Vector256.Create(factor);
            var i = 0;
            for (; i <= count - 8; i += 8)
            {
                var v = Avx.LoadVector256(velocities + i);
                v = Avx.Multiply(v, vFactor);
                Avx.Store(velocities + i, v);
            }
            for (; i < count; i++)
                velocities[i] *= factor;
        }
        else
        {
            for (var i = 0; i < count; i++)
                velocities[i] *= factor;
        }
    }

    /// <summary>
    /// Quantize note start times to a grid.
    /// Optimized: loop unrolling + precomputed constants.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Quantize(NoteBuffer buffer, Rational grid)
    {
        var count = buffer.Count;
        if (count == 0) return;

        var offsetsNum = buffer.OffsetsNumPtr;
        var offsetsDen = buffer.OffsetsDenPtr;

        var gNum = grid.Numerator;
        var gDen = grid.Denominator;

        var i = 0;

        // Loop unrolling: process 4 notes per iteration.
        // Helps the compiler optimize (fewer branch prediction misses).
        var limit = count - 3;
        for (; i < limit; i += 4)
        {
            // Note 1
            {
                var num = offsetsNum[i];
                var den = offsetsDen[i];
                var valNum = num * gDen;
                var valDen = den * gNum;
                var rounded = (valNum + (valDen >> 1)) / valDen;
                offsetsNum[i] = rounded * gNum;
                offsetsDen[i] = gDen;
            }

            // Note 2
            {
                var num = offsetsNum[i + 1];
                var den = offsetsDen[i + 1];
                var valNum = num * gDen;
                var valDen = den * gNum;
                var rounded = (valNum + (valDen >> 1)) / valDen;
                offsetsNum[i + 1] = rounded * gNum;
                offsetsDen[i + 1] = gDen;
            }

            // Note 3
            {
                var num = offsetsNum[i + 2];
                var den = offsetsDen[i + 2];
                var valNum = num * gDen;
                var valDen = den * gNum;
                var rounded = (valNum + (valDen >> 1)) / valDen;
                offsetsNum[i + 2] = rounded * gNum;
                offsetsDen[i + 2] = gDen;
            }

            // Note 4
            {
                var num = offsetsNum[i + 3];
                var den = offsetsDen[i + 3];
                var valNum = num * gDen;
                var valDen = den * gNum;
                var rounded = (valNum + (valDen >> 1)) / valDen;
                offsetsNum[i + 3] = rounded * gNum;
                offsetsDen[i + 3] = gDen;
            }
        }

        // Remainder
        for (; i < count; i++)
        {
            var num = offsetsNum[i];
            var den = offsetsDen[i];
            var valNum = num * gDen;
            var valDen = den * gNum;
            var rounded = (valNum + (valDen >> 1)) / valDen;
            offsetsNum[i] = rounded * gNum;
            offsetsDen[i] = gDen;
        }
    }
}

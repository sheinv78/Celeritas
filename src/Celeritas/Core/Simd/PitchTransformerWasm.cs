// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Celeritas.Core.Simd;

/// <summary>
/// WebAssembly SIMD pitch transformer (PackedSimd)
/// Processes 4 pitches at once using 128-bit vectors
/// Requires WASM with SIMD support enabled
/// </summary>
public sealed class PitchTransformerWasm : IPitchTransformer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Transpose(int* pitches, int count, int semitones)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            // Fallback to scalar
            for (int i = 0; i < count; i++)
                pitches[i] += semitones;
            return;
        }

        int idx = 0;
        var vSemitones = Vector128.Create(semitones);

        // Process 4 ints at a time (128-bit / 32-bit = 4)
        for (; idx <= count - 4; idx += 4)
        {
            var vPitches = Vector128.Create(
                pitches[idx],
                pitches[idx + 1],
                pitches[idx + 2],
                pitches[idx + 3]
            );

            var vResult = vPitches + vSemitones;

            pitches[idx] = vResult[0];
            pitches[idx + 1] = vResult[1];
            pitches[idx + 2] = vResult[2];
            pitches[idx + 3] = vResult[3];
        }

        // Handle remaining elements
        for (; idx < count; idx++)
        {
            pitches[idx] += semitones;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TransposeSpan(Span<int> pitches, int semitones)
    {
        // Check if Vector128 operations are hardware-accelerated (WASM SIMD)
        if (!Vector128.IsHardwareAccelerated)
        {
            // Fallback to scalar
            for (int j = 0; j < pitches.Length; j++)
                pitches[j] += semitones;
            return;
        }

        int i = 0;
        var vSemitones = Vector128.Create(semitones);

        // Process 4 ints at a time (128-bit / 32-bit = 4)
        for (; i <= pitches.Length - 4; i += 4)
        {
            var vPitches = Vector128.Create(
                pitches[i],
                pitches[i + 1],
                pitches[i + 2],
                pitches[i + 3]
            );

            var vResult = vPitches + vSemitones;

            pitches[i] = vResult[0];
            pitches[i + 1] = vResult[1];
            pitches[i + 2] = vResult[2];
            pitches[i + 3] = vResult[3];
        }

        // Handle remaining elements
        for (; i < pitches.Length; i++)
        {
            pitches[i] += semitones;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TransposeArray(int[] pitches, int semitones)
    {
        TransposeSpan(pitches.AsSpan(), semitones);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Scale(Span<int> values, int factor)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            for (int j = 0; j < values.Length; j++)
                values[j] *= factor;
            return;
        }

        int i = 0;
        var vFactor = Vector128.Create(factor);

        for (; i <= values.Length - 4; i += 4)
        {
            var vValues = Vector128.Create(
                values[i],
                values[i + 1],
                values[i + 2],
                values[i + 3]
            );

            var vResult = vValues * vFactor;

            values[i] = vResult[0];
            values[i + 1] = vResult[1];
            values[i + 2] = vResult[2];
            values[i + 3] = vResult[3];
        }

        for (; i < values.Length; i++)
        {
            values[i] *= factor;
        }
    }
}

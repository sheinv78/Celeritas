// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace Celeritas.Core.Simd;

/// <summary>
/// ARM NEON SIMD pitch transformer (AdvSimd)
/// Processes 8 pitches at once using 128-bit vectors
/// </summary>
public sealed class PitchTransformerNeon : IPitchTransformer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Transpose(int* pitches, int count, int semitones)
    {
        if (!AdvSimd.IsSupported)
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
            var vPitches = AdvSimd.LoadVector128(pitches + idx);
            var vResult = AdvSimd.Add(vPitches, vSemitones);
            AdvSimd.Store(pitches + idx, vResult);
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
        if (!AdvSimd.IsSupported)
        {
            // Fallback to scalar
            for (int i = 0; i < pitches.Length; i++)
                pitches[i] += semitones;
            return;
        }

        int idx = 0;
        var vSemitones = Vector128.Create(semitones);

        // Process 4 ints at a time (128-bit / 32-bit = 4)
        for (; idx <= pitches.Length - 4; idx += 4)
        {
            var vPitches = Vector128.Create(pitches[idx], pitches[idx + 1], pitches[idx + 2], pitches[idx + 3]);
            var vResult = AdvSimd.Add(vPitches, vSemitones);
            pitches[idx] = vResult[0];
            pitches[idx + 1] = vResult[1];
            pitches[idx + 2] = vResult[2];
            pitches[idx + 3] = vResult[3];
        }

        // Handle remaining elements
        for (; idx < pitches.Length; idx++)
        {
            pitches[idx] += semitones;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TransposeArray(int[] pitches, int semitones)
    {
        TransposeSpan(pitches.AsSpan(), semitones);
    }
}

// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Celeritas.Core.Simd;

internal sealed class PitchTransformerAvx2 : IPitchTransformer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Transpose(int* pitches, int count, int semitones)
    {
        var vSemitones = Vector256.Create(semitones);
        var i = 0;

        // Unrolling: 4 vectors per iteration (32 elements)
        var limit = count - 31;
        for (; i <= limit; i += 32)
        {
            var v0 = Avx.LoadVector256(pitches + i);
            var v1 = Avx.LoadVector256(pitches + i + 8);
            var v2 = Avx.LoadVector256(pitches + i + 16);
            var v3 = Avx.LoadVector256(pitches + i + 24);
            v0 = Avx2.Add(v0, vSemitones);
            v1 = Avx2.Add(v1, vSemitones);
            v2 = Avx2.Add(v2, vSemitones);
            v3 = Avx2.Add(v3, vSemitones);
            Avx.Store(pitches + i, v0);
            Avx.Store(pitches + i + 8, v1);
            Avx.Store(pitches + i + 16, v2);
            Avx.Store(pitches + i + 24, v3);
        }

        for (; i <= count - 8; i += 8)
        {
            var v = Avx.LoadVector256(pitches + i);
            v = Avx2.Add(v, vSemitones);
            Avx.Store(pitches + i, v);
        }

        for (; i < count; i++)
            pitches[i] += semitones;
    }
}


// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Celeritas.Core.Simd;

internal sealed class PitchTransformerAvx512 : IPitchTransformer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Transpose(int* pitches, int count, int semitones)
    {
        var vSemitones = Vector512.Create(semitones);
        var i = 0;

        // Loop unrolling: 2 vectors per iteration (32 elements)
        var limit = count - 31;
        for (; i <= limit; i += 32)
        {
            var v0 = Avx512F.LoadVector512(pitches + i);
            var v1 = Avx512F.LoadVector512(pitches + i + 16);
            v0 = Avx512F.Add(v0, vSemitones);
            v1 = Avx512F.Add(v1, vSemitones);
            Avx512F.Store(pitches + i, v0);
            Avx512F.Store(pitches + i + 16, v1);
        }

        if (i <= count - 16)
        {
            var v = Avx512F.LoadVector512(pitches + i);
            v = Avx512F.Add(v, vSemitones);
            Avx512F.Store(pitches + i, v);
            i += 16;
        }

        for (; i < count; i++)
            pitches[i] += semitones;
    }
}


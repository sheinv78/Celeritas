// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Celeritas.Core.Simd;

internal sealed class PitchTransformerSse2 : IPitchTransformer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Transpose(int* pitches, int count, int semitones)
    {
        var vSemitones = Vector128.Create(semitones);
        var i = 0;

        for (; i <= count - 4; i += 4)
        {
            var v = Sse2.LoadVector128(pitches + i);
            v = Sse2.Add(v, vSemitones);
            Sse2.Store(pitches + i, v);
        }

        for (; i < count; i++)
            pitches[i] += semitones;
    }
}


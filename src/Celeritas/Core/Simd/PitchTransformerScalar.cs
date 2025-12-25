// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core.Simd;

internal sealed class PitchTransformerScalar : IPitchTransformer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Transpose(int* pitches, int count, int semitones)
    {
        var i = 0;
        var limit = count - 3;
        for (; i <= limit; i += 4)
        {
            pitches[i] += semitones;
            pitches[i + 1] += semitones;
            pitches[i + 2] += semitones;
            pitches[i + 3] += semitones;
        }
        for (; i < count; i++)
            pitches[i] += semitones;
    }
}


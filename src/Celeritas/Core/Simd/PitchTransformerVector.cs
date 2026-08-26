// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celeritas.Core.Simd;

/// <summary>
/// Portable SIMD pitch transformer built on <see cref="Vector{T}"/>, which the JIT widens to
/// the widest vector unit available on the current CPU (AVX-512 / AVX2 / SSE2 on x86,
/// NEON on ARM, packed SIMD on WASM). One correct tail loop handles the remainder, so there
/// are no per-ISA kernels to keep in sync.
/// </summary>
internal sealed class PitchTransformerVector : IPitchTransformer
{
    public unsafe void Transpose(int* pitches, int count, int semitones)
    {
        var vSemitones = new Vector<int>(semitones);
        var vRest = new Vector<int>(MusicNotation.RestPitch);
        var width = Vector<int>.Count;
        ref var start = ref Unsafe.AsRef<int>(pitches);

        var i = 0;
        for (; i <= count - width; i += width)
        {
            var v = Vector.LoadUnsafe(ref start, (nuint)i);
            // Transposing music moves its notes; its silences stay silent. Added to blindly, a
            // rest's RestPitch (-1) became a sounding note a fifth up.
            Vector.ConditionalSelect(Vector.Equals(v, vRest), v, v + vSemitones)
                .StoreUnsafe(ref start, (nuint)i);
        }

        for (; i < count; i++)
        {
            if (pitches[i] != MusicNotation.RestPitch)
                pitches[i] += semitones;
        }
    }
}

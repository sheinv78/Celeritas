// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core.Simd;

public static class PitchTransformerFactory
{
    // Choose the optimal implementation once for the current machine.
    public static readonly IPitchTransformer Best = CreateBest();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IPitchTransformer CreateBest()
    {
        return SimdInfo.GetBest() switch
        {
            SimdInstructionSet.Avx512F => new PitchTransformerAvx512(),
            SimdInstructionSet.Avx2 => new PitchTransformerAvx2(),
            SimdInstructionSet.Sse2 => new PitchTransformerSse2(),
            SimdInstructionSet.Neon => new PitchTransformerNeon(),
            SimdInstructionSet.WasmSimd => new PitchTransformerWasm(),
            _ => new PitchTransformerScalar()
        };
    }
}


// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Celeritas.Core.Simd;

public static class PitchTransformerFactory
{
    // Choose the optimal implementation once for the current machine.
    public static readonly IPitchTransformer Best = CreateBest();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IPitchTransformer CreateBest()
    {
        // x86/x64 SIMD
        if (Avx512F.IsSupported) return new PitchTransformerAvx512();
        if (Avx2.IsSupported) return new PitchTransformerAvx2();
        if (Sse2.IsSupported) return new PitchTransformerSse2();

        // ARM SIMD (NEON)
        if (AdvSimd.IsSupported) return new PitchTransformerNeon();

        // WebAssembly SIMD (check if hardware accelerated)
        if (Vector128.IsHardwareAccelerated &&
            !Avx512F.IsSupported && !Avx2.IsSupported &&
            !Sse2.IsSupported && !AdvSimd.IsSupported)
        {
            return new PitchTransformerWasm();
        }

        // Fallback
        return new PitchTransformerScalar();
    }
}


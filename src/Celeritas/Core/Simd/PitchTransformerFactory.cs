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
        return Avx512F.IsSupported switch
        {
            // x86/x64 SIMD
            true => new PitchTransformerAvx512(),
            _ => Avx2.IsSupported switch
            {
                true => new PitchTransformerAvx2(),
                _ => Sse2.IsSupported switch
                {
                    true => new PitchTransformerSse2(),
                    _ => AdvSimd.IsSupported switch
                    {
                        // ARM SIMD (NEON)
                        true => new PitchTransformerNeon(),
                        _ => Vector128.IsHardwareAccelerated switch
                        {
                            // WebAssembly SIMD (check if hardware accelerated)
                            true when !Avx512F.IsSupported && !Avx2.IsSupported && !Sse2.IsSupported &&
                                      !AdvSimd.IsSupported => new PitchTransformerWasm(),
                            _ => new PitchTransformerScalar()
                        }
                    }
                }
            }
        };

        // Fallback
    }
}


// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Celeritas.Core.Simd;

/// <summary>
/// SIMD instruction set support flags.
/// </summary>
[Flags]
public enum SimdInstructionSet
{
    /// <summary>No SIMD support (scalar only).</summary>
    None = 0,

    /// <summary>SSE2 (x86/x64).</summary>
    Sse2 = 1 << 0,

    /// <summary>AVX2 (x86/x64).</summary>
    Avx2 = 1 << 1,

    /// <summary>AVX-512 Foundation (x86/x64).</summary>
    Avx512F = 1 << 2,

    /// <summary>ARM NEON (Advanced SIMD).</summary>
    Neon = 1 << 3,

    /// <summary>WebAssembly SIMD (128-bit).</summary>
    WasmSimd = 1 << 4
}

/// <summary>
/// Query available SIMD instruction sets on the current platform.
/// </summary>
public static class SimdInfo
{
    /// <summary>
    /// Detect all available SIMD instruction sets on the current hardware.
    /// </summary>
    public static SimdInstructionSet Detect()
    {
        var result = SimdInstructionSet.None;

        // x86/x64 instruction sets
        if (Avx512F.IsSupported)
        {
            result |= SimdInstructionSet.Avx512F;
        }

        if (Avx2.IsSupported)
        {
            result |= SimdInstructionSet.Avx2;
        }

        if (Sse2.IsSupported)
        {
            result |= SimdInstructionSet.Sse2;
        }

        // ARM NEON
        if (AdvSimd.IsSupported)
        {
            result |= SimdInstructionSet.Neon;
        }

        // WebAssembly SIMD (heuristic check)
        if (Vector128.IsHardwareAccelerated &&
            !Avx512F.IsSupported && !Avx2.IsSupported &&
            !Sse2.IsSupported && !AdvSimd.IsSupported)
        {
            result |= SimdInstructionSet.WasmSimd;
        }

        return result;
    }

    /// <summary>
    /// Get the best (highest-performance) available instruction set.
    /// </summary>
    public static SimdInstructionSet GetBest()
    {
        if (Avx512F.IsSupported)
        {
            return SimdInstructionSet.Avx512F;
        }

        if (Avx2.IsSupported)
        {
            return SimdInstructionSet.Avx2;
        }

        if (Sse2.IsSupported)
        {
            return SimdInstructionSet.Sse2;
        }

        if (AdvSimd.IsSupported)
        {
            return SimdInstructionSet.Neon;
        }

        if (Vector128.IsHardwareAccelerated &&
            !Avx512F.IsSupported && !Avx2.IsSupported &&
            !Sse2.IsSupported && !AdvSimd.IsSupported)
        {
            return SimdInstructionSet.WasmSimd;
        }

        return SimdInstructionSet.None;
    }

    /// <summary>
    /// Check if a specific instruction set is available.
    /// </summary>
    public static bool IsSupported(SimdInstructionSet instructionSet)
    {
        return (Detect() & instructionSet) == instructionSet;
    }

    /// <summary>
    /// Get human-readable description of detected SIMD capabilities.
    /// </summary>
    public static string GetDescription()
    {
        var detected = Detect();
        if (detected == SimdInstructionSet.None)
        {
            return "No SIMD support (scalar only)";
        }

        var parts = new List<string>();

        if ((detected & SimdInstructionSet.Avx512F) != 0)
        {
            parts.Add("AVX-512");
        }

        if ((detected & SimdInstructionSet.Avx2) != 0)
        {
            parts.Add("AVX2");
        }

        if ((detected & SimdInstructionSet.Sse2) != 0)
        {
            parts.Add("SSE2");
        }

        if ((detected & SimdInstructionSet.Neon) != 0)
        {
            parts.Add("NEON");
        }

        if ((detected & SimdInstructionSet.WasmSimd) != 0)
        {
            parts.Add("WebAssembly SIMD");
        }

        return string.Join(", ", parts);
    }
}

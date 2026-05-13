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
    private static readonly (SimdInstructionSet InstructionSet, bool IsSupported, string Description)[] InstructionSetDescriptions =
    [
        (SimdInstructionSet.Avx512F, Avx512F.IsSupported, "AVX-512"),
        (SimdInstructionSet.Avx2, Avx2.IsSupported, "AVX2"),
        (SimdInstructionSet.Sse2, Sse2.IsSupported, "SSE2"),
        (SimdInstructionSet.Neon, AdvSimd.IsSupported, "NEON"),
        (SimdInstructionSet.WasmSimd,
            Vector128.IsHardwareAccelerated && !Avx512F.IsSupported && !Avx2.IsSupported && !Sse2.IsSupported && !AdvSimd.IsSupported,
            "WebAssembly SIMD")
    ];

    /// <summary>
    /// Detect all available SIMD instruction sets on the current hardware.
    /// </summary>
    public static SimdInstructionSet Detect()
    {
        return InstructionSetDescriptions
            .Where(entry => entry.IsSupported)
            .Aggregate(SimdInstructionSet.None, (acc, entry) => acc | entry.InstructionSet);
    }

    /// <summary>
    /// Get the best (highest-performance) available instruction set.
    /// </summary>
    public static SimdInstructionSet GetBest()
    {
        return Detect() switch
        {
            var detected when (detected & SimdInstructionSet.Avx512F) != 0 => SimdInstructionSet.Avx512F,
            var detected when (detected & SimdInstructionSet.Avx2) != 0 => SimdInstructionSet.Avx2,
            var detected when (detected & SimdInstructionSet.Sse2) != 0 => SimdInstructionSet.Sse2,
            var detected when (detected & SimdInstructionSet.Neon) != 0 => SimdInstructionSet.Neon,
            var detected when (detected & SimdInstructionSet.WasmSimd) != 0 => SimdInstructionSet.WasmSimd,
            _ => SimdInstructionSet.None
        };
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
        return Detect() switch
        {
            SimdInstructionSet.None => "No SIMD support (scalar only)",
            var detected => string.Join(", ", GetDescriptions(detected))
        };
    }

    private static IEnumerable<string> GetDescriptions(SimdInstructionSet detected) =>
        InstructionSetDescriptions
            .Where(entry => (detected & entry.InstructionSet) != 0)
            .Select(entry => entry.Description);
}

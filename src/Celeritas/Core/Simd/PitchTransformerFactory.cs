// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;

namespace Celeritas.Core.Simd;

internal static class PitchTransformerFactory
{
    /// <summary>
    /// The pitch transformer for the current machine, chosen once at startup:
    /// a portable <see cref="Vector{T}"/> kernel when SIMD is hardware-accelerated
    /// (the JIT targets the widest available unit), otherwise a scalar fallback.
    /// Use <see cref="SimdInfo"/> to report which instruction sets are present.
    /// </summary>
    public static readonly IPitchTransformer Best =
        Vector.IsHardwareAccelerated ? new PitchTransformerVector() : new PitchTransformerScalar();
}

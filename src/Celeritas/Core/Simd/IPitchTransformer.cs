// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Simd;

public interface IPitchTransformer
{
    /// <summary>
    /// In-place transpose of MIDI pitches.
    /// </summary>
    unsafe void Transpose(int* pitches, int count, int semitones);
}


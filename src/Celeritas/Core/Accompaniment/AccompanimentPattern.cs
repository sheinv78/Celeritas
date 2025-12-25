// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Accompaniment;

/// <summary>
/// Simple accompaniment patterns.
/// </summary>
public enum AccompanimentPattern : byte
{
    /// <summary>
    /// Sustained bass + sustained chord for the whole harmonic segment.
    /// </summary>
    Block,

    /// <summary>
    /// Alternating bass then chord tones on a fixed subdivision.
    /// </summary>
    Arpeggio
}

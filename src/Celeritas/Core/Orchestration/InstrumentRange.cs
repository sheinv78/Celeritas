// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Orchestration;

/// <summary>
/// MIDI pitch range for an instrument/part.
/// </summary>
public readonly record struct InstrumentRange(int MinPitch, int MaxPitch)
{
    public bool Contains(int pitch) => pitch >= MinPitch && pitch <= MaxPitch;
}

// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// A single note event stored in a <see cref="NoteBuffer"/>.
/// </summary>
public readonly struct NoteEvent(int pitch, Rational offset, Rational duration, float velocity = 0.8f)
{
    public readonly int Pitch = pitch;
    public readonly Rational Offset = offset;
    public readonly Rational Duration = duration;
    public readonly float Velocity = velocity;
}

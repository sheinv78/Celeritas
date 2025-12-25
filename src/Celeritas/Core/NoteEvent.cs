// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// A single note event stored in a <see cref="NoteBuffer"/>.
/// </summary>
public readonly struct NoteEvent
{
    public readonly int Pitch;
    public readonly Rational Offset;
    public readonly Rational Duration;
    public readonly float Velocity;

    public NoteEvent(int pitch, Rational offset, Rational duration, float velocity = 0.8f)
    {
        Pitch = pitch;
        Offset = offset;
        Duration = duration;
        Velocity = velocity;
    }
}

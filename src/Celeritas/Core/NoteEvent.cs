// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// A single note event stored in a <see cref="NoteBuffer"/>.
/// <para>
/// Time convention: <see cref="Offset"/> and <see cref="Duration"/> are fractions of a
/// WHOLE note — a quarter note is <c>Rational.Quarter</c> (1/4), one 4/4 measure is 1.
/// This is the unit produced by <c>MusicNotation.Parse</c> and consumed by all analyzers
/// and by <c>MidiIo</c>.
/// </para>
/// </summary>
public readonly struct NoteEvent(int pitch, Rational offset, Rational duration, float velocity = 0.8f)
{
    public readonly int Pitch = pitch;
    public readonly Rational Offset = offset;
    public readonly Rational Duration = duration;
    public readonly float Velocity = velocity;
}

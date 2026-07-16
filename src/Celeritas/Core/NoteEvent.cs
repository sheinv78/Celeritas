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
/// <param name="pitch">MIDI pitch number (middle C = 60).</param>
/// <param name="offset">Start time, in whole-note units, from the start of the buffer.</param>
/// <param name="duration">Sounding length, in whole-note units.</param>
/// <param name="velocity">Normalized loudness in the range 0..1 (default 0.8).</param>
public readonly struct NoteEvent(int pitch, Rational offset, Rational duration, float velocity = 0.8f)
{
    /// <summary>MIDI pitch number (middle C = 60).</summary>
    public readonly int Pitch = pitch;

    /// <summary>Start time, in whole-note units, from the start of the buffer.</summary>
    public readonly Rational Offset = offset;

    /// <summary>Sounding length, in whole-note units.</summary>
    public readonly Rational Duration = duration;

    /// <summary>Normalized loudness in the range 0..1.</summary>
    public readonly float Velocity = velocity;
}

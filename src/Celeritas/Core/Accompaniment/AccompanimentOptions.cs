// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Accompaniment;

/// <summary>
/// Configuration for accompaniment generation.
/// </summary>
/// <param name="Pattern">Accompaniment pattern (block chords, arpeggio, etc.).</param>
/// <param name="BassOctave">Octave for the bass line.</param>
/// <param name="ChordOctave">Octave for the chord voicing.</param>
/// <param name="BassVelocity">Bass note velocity, 0..1.</param>
/// <param name="ChordVelocity">Chord note velocity, 0..1.</param>
/// <param name="Subdivision">Rhythmic subdivision in whole-note units.</param>
/// <param name="MaxChordTones">Maximum number of chord tones to sound at once.</param>
public readonly record struct AccompanimentOptions(
    AccompanimentPattern Pattern,
    int BassOctave,
    int ChordOctave,
    float BassVelocity,
    float ChordVelocity,
    Rational Subdivision,
    int MaxChordTones)
{
    /// <summary>Default accompaniment options: block chords, bass octave 2, chord octave 4, eighth-note subdivision.</summary>
    public static AccompanimentOptions Default => new(
        Pattern: AccompanimentPattern.Block,
        BassOctave: 2,
        ChordOctave: 4,
        BassVelocity: 0.8f,
        ChordVelocity: 0.65f,
        Subdivision: Rational.Eighth,
        MaxChordTones: 4);
}

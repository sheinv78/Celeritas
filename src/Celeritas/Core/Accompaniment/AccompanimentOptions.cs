// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Accompaniment;

/// <summary>
/// Configuration for accompaniment generation.
/// </summary>
public readonly record struct AccompanimentOptions(
    AccompanimentPattern Pattern,
    int BassOctave,
    int ChordOctave,
    float BassVelocity,
    float ChordVelocity,
    Rational Subdivision,
    int MaxChordTones)
{
    public static AccompanimentOptions Default => new(
        Pattern: AccompanimentPattern.Block,
        BassOctave: 2,
        ChordOctave: 4,
        BassVelocity: 0.8f,
        ChordVelocity: 0.65f,
        Subdivision: Rational.Eighth,
        MaxChordTones: 4);
}

// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.FiguredBass;

/// <summary>
/// Options for figured bass realization with voice-leading customization.
/// </summary>
public sealed class FiguredBassRealizerOptions : FiguredBassOptions
{
    /// <summary>
    /// Allow upper voices to cross each other.
    /// If false, upper voices will be ordered low→high.
    /// </summary>
    public bool AllowVoiceCrossing { get; init; } = false;

    /// <summary>
    /// Maximum desired movement per upper voice in semitones between successive symbols.
    /// This is a soft constraint: the realizer always chooses the octave placement of
    /// the required pitch class closest to the voice's previous pitch, which satisfies
    /// the limit whenever it is satisfiable. When no placement can honor the limit
    /// (e.g. <c>MaxVoiceMovement = 0</c> across a chord change), the closest placement
    /// is used instead of failing. If null, no movement preference is implied.
    /// </summary>
    public int? MaxVoiceMovement { get; init; } = null;
}

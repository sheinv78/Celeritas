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
    /// Maximum allowed movement per upper voice in semitones between successive symbols.
    /// If null, no movement constraint is enforced.
    /// </summary>
    public int? MaxVoiceMovement { get; init; } = null;
}

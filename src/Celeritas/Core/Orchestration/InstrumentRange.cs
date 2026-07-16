// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core.Orchestration;

/// <summary>
/// MIDI pitch range for an instrument/part.
/// </summary>
/// <remarks>
/// Both bounds must be MIDI pitches, and the range must not be inverted. Unvalidated, this type
/// could describe a range no instrument has: <c>(int.MaxValue, int.MaxValue)</c> was perfectly
/// constructible, and wedged <see cref="OrchestrationMapper"/> in an infinite loop.
///
/// This covers construction only. <c>default(InstrumentRange)</c> is (0, 0) and <c>with { }</c>
/// assigns without re-checking — as they do for every C# record struct — so consumers have to stay
/// correct for any bounds regardless of this. <c>OrchestrationMapper.ClampToRange</c> does.
/// </remarks>
/// <param name="MinPitch">Lowest playable MIDI pitch, 0..127.</param>
/// <param name="MaxPitch">Highest playable MIDI pitch, 0..127, not below <paramref name="MinPitch"/>.</param>
/// <exception cref="ArgumentOutOfRangeException">
/// A bound is outside [0, 127], or <paramref name="MaxPitch"/> is below <paramref name="MinPitch"/>.
/// </exception>
public readonly record struct InstrumentRange(int MinPitch, int MaxPitch)
{
    /// <summary>Lowest playable MIDI pitch.</summary>
    public int MinPitch { get; init; } = ThrowIfNotMidiPitch(MinPitch);

    /// <summary>Highest playable MIDI pitch.</summary>
    public int MaxPitch { get; init; } = ThrowIfNotBelowMin(ThrowIfNotMidiPitch(MaxPitch), MinPitch);

    /// <summary>Returns whether <paramref name="pitch"/> lies within the inclusive range.</summary>
    public bool Contains(int pitch) => pitch >= MinPitch && pitch <= MaxPitch;

    private static int ThrowIfNotMidiPitch(int pitch,
        [CallerArgumentExpression(nameof(pitch))] string? name = null)
    {
        if ((uint)pitch > 127)
            throw new ArgumentOutOfRangeException(name, pitch, "Must be a MIDI pitch in [0, 127].");

        return pitch;
    }

    private static int ThrowIfNotBelowMin(int maxPitch, int minPitch)
    {
        if (maxPitch < minPitch)
            throw new ArgumentOutOfRangeException(nameof(MaxPitch), maxPitch,
                $"Must not be below MinPitch ({minPitch}).");

        return maxPitch;
    }
}

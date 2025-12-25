// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Result of melody harmonization.
/// </summary>
public sealed class HarmonizationResult
{
    /// <summary>
    /// The detected or specified key.
    /// </summary>
    public KeySignature Key { get; init; }

    /// <summary>
    /// Chord assignments for each time slice.
    /// </summary>
    public IReadOnlyList<ChordAssignment> Chords { get; init; } = [];

    /// <summary>
    /// Total cost of this harmonization (lower = better).
    /// </summary>
    public float TotalCost { get; init; }

    /// <summary>
    /// Get chord symbols as strings.
    /// </summary>
    public IEnumerable<string> GetSymbols() => Chords.Select(c => c.Chord.ToString());
}

/// <summary>
/// A chord assigned to a specific time slice.
/// </summary>
public readonly record struct ChordAssignment(
    Rational Start,
    Rational End,
    ChordInfo Chord,
    int[] Pitches,
    float Cost,
    string? Rationale = null);

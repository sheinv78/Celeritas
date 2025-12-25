// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Determines where chord changes occur (harmonic rhythm).
/// Implement this to customize chord change placement.
/// </summary>
public interface IHarmonicRhythmStrategy
{
    /// <summary>
    /// Segment a melody into slices, each representing one chord.
    /// </summary>
    /// <param name="melody">The melody notes to segment</param>
    /// <returns>Time slices where each slice gets one chord</returns>
    IReadOnlyList<MelodySlice> Segment(ReadOnlySpan<NoteEvent> melody);
}

/// <summary>
/// A slice of melody notes that will receive one chord.
/// </summary>
public readonly record struct MelodySlice(
    Rational Start,
    Rational End,
    int[] Pitches,
    bool IsStrongBeat);

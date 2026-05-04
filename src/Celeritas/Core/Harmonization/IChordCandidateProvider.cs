// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Provides chord candidates for a given melody slice.
/// Implement this interface to customize how chords are generated.
/// </summary>
public interface IChordCandidateProvider
{
    /// <summary>
    /// Generate candidate chords for a melody segment.
    /// </summary>
    /// <param name="melodyPitches">Pitch classes (0-11) active in this segment</param>
    /// <param name="key">Current key signature</param>
    /// <param name="context">Optional context from previous harmonization</param>
    /// <returns>Candidate chords with their associated costs</returns>
    IEnumerable<ChordCandidate> GetCandidates(
        int[] melodyPitches,
        KeySignature key,
        HarmonizationContext? context = null);
}

/// <summary>
/// A chord candidate with an associated base cost.
/// Lower cost = more likely to be chosen.
/// </summary>
public readonly record struct ChordCandidate(
    ChordInfo Chord,
    int[] Pitches,
    float BaseCost);

/// <summary>
/// Context passed between harmonization steps.
/// </summary>
public class HarmonizationContext
{
    public int StepIndex { get; set; }
}

// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Scores transitions between chords.
/// Implement this to customize harmonic preferences (voice leading, function, etc.).
/// </summary>
public interface ITransitionScorer
{
    /// <summary>
    /// Calculate the cost of transitioning from one chord to another.
    /// Lower cost = better transition.
    /// </summary>
    float ScoreTransition(
        ChordCandidate from,
        ChordCandidate to,
        KeySignature key);
}

/// <summary>
/// Scores how well a chord fits the melody at a given moment.
/// </summary>
public interface IMelodyFitScorer
{
    /// <summary>
    /// Calculate the cost of using this chord for the given melody pitches.
    /// Lower cost = better fit.
    /// </summary>
    float ScoreFit(
        ChordCandidate chord,
        ReadOnlySpan<int> melodyPitches,
        bool isStrongBeat);
}

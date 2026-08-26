// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Default scorer: prefers functional progressions and smooth bass motion.
/// </summary>
public sealed class DefaultTransitionScorer : ITransitionScorer, IMelodyFitScorer
{
    /// <summary>
    /// Scores the cost of moving from one chord to another: rewards strong root motion,
    /// functional T-PD-D-T flow, and common tones; penalizes regressive motion. Lower is better.
    /// </summary>
    public float ScoreTransition(ChordCandidate from, ChordCandidate to, KeySignature key)
    {
        var cost = 0f;

        // 1. Root motion analysis
        var fromRoot = from.Chord.RootPitchClass;
        var toRoot = to.Chord.RootPitchClass;
        var interval = (toRoot - fromRoot + 12) % 12;

        // Prefer strong root motions (4th up, 5th down, step)
        cost += interval switch
        {
            5 => 0.0f,   // 4th up (e.g., I->IV, V->I)
            7 => 0.0f,   // 5th up (e.g., IV->I as plagal)
            2 => 0.1f,   // step up
            10 => 0.1f,  // step down
            3 => 0.2f,   // minor 3rd
            4 => 0.2f,   // major 3rd
            0 => 0.3f,   // same chord (repetition)
            _ => 0.5f    // tritone or unusual
        };

        // 2. Functional progression bonus
        var fromFunc = GetFunction(from.Chord, key);
        var toFunc = GetFunction(to.Chord, key);

        // T->PD->D->T is the ideal flow
        if (fromFunc == HarmonicFunction.Tonic && toFunc == HarmonicFunction.Subdominant)
            cost -= 0.1f;
        if (fromFunc == HarmonicFunction.Subdominant && toFunc == HarmonicFunction.Dominant)
            cost -= 0.2f;
        if (fromFunc == HarmonicFunction.Dominant && toFunc == HarmonicFunction.Tonic)
            cost -= 0.3f; // V->I is strong

        // Avoid regressive motion (D->PD)
        if (fromFunc == HarmonicFunction.Dominant && toFunc == HarmonicFunction.Subdominant)
            cost += 0.3f;

        // 3. Voice leading (simple: count common tones)
        var fromMask = GetChordMask(from.Pitches);
        var toMask = GetChordMask(to.Pitches);
        var commonTones = BitOperations.PopCount((uint)(fromMask & toMask));
        cost -= commonTones * 0.05f;

        return Math.Max(0, cost);
    }

    /// <summary>
    /// Scores how well a chord fits the melody pitches, penalizing non-chord tones more heavily
    /// on strong beats. Lower is better.
    /// </summary>
    public float ScoreFit(ChordCandidate chord, ReadOnlySpan<int> melodyPitches, bool isStrongBeat)
    {
        var cost = chord.BaseCost;
        var chordMask = GetChordMask(chord.Pitches);

        foreach (var p in melodyPitches)
        {
            // Fold, not `%`: a melody pitch below zero — which MusicMath.Transpose documents it
            // can produce — gave a negative shift and tested a bit belonging to another note.
            var pc = PitchMath.Fold(p);
            var inChord = (chordMask & (1 << pc)) != 0;

            if (!inChord)
            {
                // Non-chord tone penalty (higher on strong beats)
                cost += isStrongBeat ? 0.5f : 0.2f;
            }
        }

        return cost;
    }

    private static HarmonicFunction GetFunction(ChordInfo chord, KeySignature key)
    {
        var interval = (chord.RootPitchClass - key.Root + 12) % 12;

        return key.IsMajor switch
        {
            true => interval switch
            {
                0 or 4 or 9 => HarmonicFunction.Tonic, // I, iii, vi
                2 or 5 => HarmonicFunction.Subdominant, // ii, IV
                7 or 11 => HarmonicFunction.Dominant, // V, vii°
                _ => HarmonicFunction.Tonic
            },
            _ => interval switch
            {
                0 or 3 or 8 => HarmonicFunction.Tonic, // i, III, VI
                2 or 5 => HarmonicFunction.Subdominant, // ii°, iv
                7 or 10 => HarmonicFunction.Dominant, // v/V, VII
                _ => HarmonicFunction.Tonic
            }
        };
    }

    /// <summary>
    /// The pitch-class mask of a candidate's tones.
    /// </summary>
    /// <remarks>
    /// A <see cref="ChordCandidate"/> is a struct, so <c>default</c> is a value any caller can
    /// hand over and its Pitches are null there — this dereferenced them. And the fold is
    /// PitchMath.Fold rather than `%`: C# keeps the sign, so a pitch below zero shifted by a
    /// negative amount and set a bit that has nothing to do with the note.
    /// </remarks>
    private static ushort GetChordMask(int[]? pitches)
    {
        if (pitches is null) return 0;

        ushort mask = 0;
        foreach (var p in pitches)
            mask |= (ushort)(1 << PitchMath.Fold(p));
        return mask;
    }
}

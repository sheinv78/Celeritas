// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Default scorer: prefers functional progressions and smooth bass motion.
/// </summary>
public sealed class DefaultTransitionScorer : ITransitionScorer, IMelodyFitScorer
{
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

    public float ScoreFit(ChordCandidate chord, ReadOnlySpan<int> melodyPitches, bool isStrongBeat)
    {
        var cost = chord.BaseCost;
        var chordMask = GetChordMask(chord.Pitches);

        foreach (var p in melodyPitches)
        {
            var pc = p % 12;
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

        if (key.IsMajor)
        {
            return interval switch
            {
                0 or 4 or 9 => HarmonicFunction.Tonic,      // I, iii, vi
                2 or 5 => HarmonicFunction.Subdominant,     // ii, IV
                7 or 11 => HarmonicFunction.Dominant,       // V, vii°
                _ => HarmonicFunction.Tonic
            };
        }
        else
        {
            return interval switch
            {
                0 or 3 or 8 => HarmonicFunction.Tonic,      // i, III, VI
                2 or 5 => HarmonicFunction.Subdominant,     // ii°, iv
                7 or 10 => HarmonicFunction.Dominant,       // v/V, VII
                _ => HarmonicFunction.Tonic
            };
        }
    }

    private static ushort GetChordMask(int[] pitches)
    {
        ushort mask = 0;
        foreach (var p in pitches)
            mask |= (ushort)(1 << (p % 12));
        return mask;
    }
}

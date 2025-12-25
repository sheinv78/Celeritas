// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Default provider: generates diatonic chords that contain the melody note.
/// </summary>
public sealed class DefaultChordCandidateProvider : IChordCandidateProvider
{
    // Diatonic chord templates (scale degree -> intervals from root)
    private static readonly int[][] MajorDiatonicIntervals =
    [
        [0, 4, 7],      // I   - major
        [0, 3, 7],      // ii  - minor
        [0, 3, 7],      // iii - minor
        [0, 4, 7],      // IV  - major
        [0, 4, 7],      // V   - major
        [0, 3, 7],      // vi  - minor
        [0, 3, 6]       // vii°- diminished
    ];

    private static readonly int[] MajorScaleDegrees = [0, 2, 4, 5, 7, 9, 11];

    private static readonly int[][] MinorDiatonicIntervals =
    [
        [0, 3, 7],      // i   - minor
        [0, 3, 6],      // ii° - diminished
        [0, 4, 7],      // III - major
        [0, 3, 7],      // iv  - minor
        [0, 3, 7],      // v   - minor (or V major with raised 7th)
        [0, 4, 7],      // VI  - major
        [0, 4, 7]       // VII - major
    ];

    private static readonly int[] MinorScaleDegrees = [0, 2, 3, 5, 7, 8, 10];

    public IEnumerable<ChordCandidate> GetCandidates(
        int[] melodyPitches,
        KeySignature key,
        HarmonizationContext? context = null)
    {
        if (melodyPitches.Length == 0)
            yield break;

        var melodyMask = GetPitchMask(melodyPitches);
        var scaleDegrees = key.IsMajor ? MajorScaleDegrees : MinorScaleDegrees;
        var intervals = key.IsMajor ? MajorDiatonicIntervals : MinorDiatonicIntervals;

        for (var degree = 0; degree < 7; degree++)
        {
            var root = (key.Root + scaleDegrees[degree]) % 12;
            var chordIntervals = intervals[degree];
            var pitches = new int[chordIntervals.Length];
            ushort chordMask = 0;

            for (var i = 0; i < chordIntervals.Length; i++)
            {
                var pc = (root + chordIntervals[i]) % 12;
                pitches[i] = 60 + pc; // Octave 4
                chordMask |= (ushort)(1 << pc);
            }

            // Check if chord contains any melody note
            if ((chordMask & melodyMask) == 0)
                continue;

            var chord = ChordAnalyzer.Identify(pitches);
            if (chord.Quality == ChordQuality.Unknown)
                continue;

            // Base cost: favor tonic/dominant, slightly penalize others
            var baseCost = degree switch
            {
                0 => 0.0f,  // I - tonic
                4 => 0.1f,  // V - dominant
                3 => 0.2f,  // IV - subdominant
                5 => 0.3f,  // vi - relative minor
                1 => 0.4f,  // ii - pre-dominant
                2 => 0.5f,  // iii
                6 => 0.6f,  // vii° - leading tone
                _ => 1.0f
            };

            // Bonus if melody note is chord tone (root/3rd/5th)
            var melodyInChord = CountMatchingBits(melodyMask, chordMask);
            baseCost -= melodyInChord * 0.1f;

            var romanNumeral = ToRomanNumeral(degree, key.IsMajor);
            yield return new ChordCandidate(chord, pitches, baseCost, romanNumeral);
        }
    }

    private static ushort GetPitchMask(int[] pitches)
    {
        ushort mask = 0;
        foreach (var p in pitches)
            mask |= (ushort)(1 << (p % 12));
        return mask;
    }

    private static int CountMatchingBits(ushort a, ushort b)
    {
        return System.Numerics.BitOperations.PopCount((uint)(a & b));
    }

    private static string ToRomanNumeral(int degree, bool isMajor)
    {
        var numerals = isMajor
            ? new[] { "I", "ii", "iii", "IV", "V", "vi", "vii°" }
            : new[] { "i", "ii°", "III", "iv", "v", "VI", "VII" };
        return numerals[degree];
    }
}

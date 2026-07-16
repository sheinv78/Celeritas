// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celeritas.Core.VoiceLeading;

/// <summary>
/// High-performance voice leading rule checker.
/// Uses bitwise operations and SIMD where possible for speed.
/// </summary>
public static class VoiceLeadingRules
{
    // Interval classes (mod 12) — only those actually used
    private const int Unison = 0;
    private const int Tritone = 6;
    private const int PerfectFifth = 7;

    // Penalty weights for different violations
    private static readonly float[] ViolationPenalties = new float[16];

    static VoiceLeadingRules()
    {
        // Initialize penalties (higher = worse)
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.ParallelFifths)] = 100f;
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.ParallelOctaves)] = 100f;
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.HiddenFifths)] = 30f;
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.HiddenOctaves)] = 30f;
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.VoiceCrossing)] = 50f;
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.VoiceOverlap)] = 40f;
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.AugmentedInterval)] = 60f;
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.LargeLeap)] = 25f;
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.UnresolvedLeadingTone)] = 45f;
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.UnresolvedSeventh)] = 35f;
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.ExcessiveSpacing)] = 20f;
        ViolationPenalties[BitOperations.Log2((uint)VoiceLeadingViolation.DoubledLeadingTone)] = 55f;
    }

    /// <summary>
    /// Check all voice leading rules between two consecutive voicings.
    /// </summary>
    /// <param name="from">The voicing being left.</param>
    /// <param name="to">The voicing being moved to.</param>
    /// <param name="keyRoot">Pitch class of the tonic, 0=C .. 11=B. Folded into that range, as
    /// elsewhere in the engine, so -1 is B and 12 is C.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VoiceLeadingCheck Check(Voicing from, Voicing to, int keyRoot = 0)
        => Check(from, to, NormalizePitchClass(keyRoot), GetChordalSeventhPitchClass(from));

    /// <summary>
    /// Fold a pitch class into [0, 12). Applied at the public boundary only: the internal
    /// overload below is called once per candidate by the DP solver and takes the folded value.
    /// </summary>
    /// <remarks>
    /// An unfolded root does not fail here, it just answers differently — keyRoot 99 reported a
    /// DoubledLeadingTone violation that keyRoot -1 did not, both for the same pair of voicings.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NormalizePitchClass(int pitchClass) => PitchMath.Fold(pitchClass);

    /// <summary>
    /// Overload for hot paths (e.g. the DP solver) that evaluate many 'to' candidates against
    /// the same 'from': the chordal-seventh pitch class of 'from' depends only on 'from', so it
    /// can be computed once per source voicing and reused instead of re-running chord
    /// identification for every candidate. Pass the result of
    /// <see cref="GetChordalSeventhPitchClass"/> for <paramref name="fromChordalSeventhPc"/>.
    /// </summary>
    internal static VoiceLeadingCheck Check(Voicing from, Voicing to, int keyRoot, int fromChordalSeventhPc)
    {
        var violations = VoiceLeadingViolation.None;

        // Check parallel fifths and octaves (all voice pairs)
        violations |= CheckParallels(from, to);

        // Check hidden fifths/octaves (outer voices only)
        violations |= CheckHiddenParallels(from, to);

        // Check voice crossing and overlap
        violations |= CheckCrossingAndOverlap(from, to);

        // Check melodic intervals
        violations |= CheckMelodicIntervals(from, to);

        // Check spacing
        violations |= CheckSpacing(to);

        // Check resolution rules (leading tone, seventh)
        violations |= CheckResolutions(from, to, keyRoot, fromChordalSeventhPc);

        // Check doubling
        violations |= CheckDoubling(to, keyRoot);

        // Calculate total penalty
        var penalty = CalculatePenalty(violations);

        return new VoiceLeadingCheck(violations, penalty);
    }

    /// <summary>
    /// Fast check for parallel fifths and octaves using bitwise ops.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VoiceLeadingViolation CheckParallels(Voicing from, Voicing to)
    {
        var violations = VoiceLeadingViolation.None;

        // Check all 6 voice pairs: (B,T), (B,A), (B,S), (T,A), (T,S), (A,S)
        Span<(VoicePart, VoicePart)> pairs = [(VoicePart.Bass, VoicePart.Tenor), (VoicePart.Bass, VoicePart.Alto),
            (VoicePart.Bass, VoicePart.Soprano), (VoicePart.Tenor, VoicePart.Alto),
            (VoicePart.Tenor, VoicePart.Soprano), (VoicePart.Alto, VoicePart.Soprano)];

        foreach (var (v1, v2) in pairs)
        {
            var interval1 = IntervalClass(from[v1], from[v2]);
            var interval2 = IntervalClass(to[v1], to[v2]);

            // Both voices must move for parallel motion
            var voice1Moved = from[v1] != to[v1];
            var voice2Moved = from[v2] != to[v2];

            if (voice1Moved && voice2Moved)
            {
                // Consecutive perfect intervals between the same voice pair are forbidden
                // whether the voices move in similar OR contrary motion ("antiparallel"
                // fifths/octaves, e.g. P5 -> P5 with the voices moving apart, are equally
                // banned in common-practice voice leading). Oblique motion (one voice
                // stationary) is excluded by the voiceMoved guards above.

                // Consecutive perfect 5ths
                if (interval1 == PerfectFifth && interval2 == PerfectFifth)
                {
                    violations |= VoiceLeadingViolation.ParallelFifths;
                }

                // Consecutive octaves/unisons
                if (interval1 == Unison && interval2 == Unison)
                {
                    violations |= VoiceLeadingViolation.ParallelOctaves;
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Check hidden (direct) fifths and octaves in outer voices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VoiceLeadingViolation CheckHiddenParallels(Voicing from, Voicing to)
    {
        var violations = VoiceLeadingViolation.None;

        // Only check outer voices (Bass and Soprano)
        var bassMotion = Math.Sign(to.Bass - from.Bass);
        var sopMotion = Math.Sign(to.Soprano - from.Soprano);

        // Hidden parallels require similar motion AND soprano moving by leap (> 2 semitones)
        if (bassMotion == sopMotion && bassMotion != 0)
        {
            var sopInterval = Math.Abs(to.Soprano - from.Soprano);
            if (sopInterval > 2) // Soprano leaps
            {
                var resultInterval = IntervalClass(to.Bass, to.Soprano);

                if (resultInterval == PerfectFifth)
                    violations |= VoiceLeadingViolation.HiddenFifths;

                if (resultInterval == Unison)
                    violations |= VoiceLeadingViolation.HiddenOctaves;
            }
        }

        return violations;
    }

    /// <summary>
    /// Check for voice crossing (voice goes above/below adjacent voice)
    /// and voice overlap (voice moves past previous position of adjacent voice).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VoiceLeadingViolation CheckCrossingAndOverlap(Voicing from, Voicing to)
    {
        var violations = VoiceLeadingViolation.None;

        // Voice crossing in result chord
        if (to.Bass > to.Tenor || to.Tenor > to.Alto || to.Alto > to.Soprano)
        {
            violations |= VoiceLeadingViolation.VoiceCrossing;
        }

        // Voice overlap: voice moves past previous position of adjacent voice
        // Bass moves above previous Tenor
        if (to.Bass > from.Tenor) violations |= VoiceLeadingViolation.VoiceOverlap;
        // Tenor moves below previous Bass or above previous Alto
        if (to.Tenor < from.Bass || to.Tenor > from.Alto) violations |= VoiceLeadingViolation.VoiceOverlap;
        // Alto moves below previous Tenor or above previous Soprano
        if (to.Alto < from.Tenor || to.Alto > from.Soprano) violations |= VoiceLeadingViolation.VoiceOverlap;
        // Soprano moves below previous Alto
        if (to.Soprano < from.Alto) violations |= VoiceLeadingViolation.VoiceOverlap;

        return violations;
    }

    /// <summary>
    /// Check melodic intervals for each voice.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VoiceLeadingViolation CheckMelodicIntervals(Voicing from, Voicing to)
    {
        var violations = VoiceLeadingViolation.None;

        for (var v = 0; v < 4; v++)
        {
            var voice = (VoicePart)v;
            var interval = Math.Abs(to[voice] - from[voice]);

            // Large leap (> octave)
            if (interval > 12)
            {
                violations |= VoiceLeadingViolation.LargeLeap;
            }

            // Augmented intervals (augmented 2nd = 3 semitones in certain contexts,
            // but we simplify to tritone as melodic interval)
            if (interval == Tritone)
            {
                violations |= VoiceLeadingViolation.AugmentedInterval;
            }
        }

        return violations;
    }

    /// <summary>
    /// Check spacing between adjacent upper voices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VoiceLeadingViolation CheckSpacing(Voicing voicing)
    {
        // Upper voices (T-A, A-S) should not exceed an octave
        if (voicing.Alto - voicing.Tenor > 12 || voicing.Soprano - voicing.Alto > 12)
        {
            return VoiceLeadingViolation.ExcessiveSpacing;
        }

        // Bass can be farther from Tenor (up to 2 octaves is acceptable)

        return VoiceLeadingViolation.None;
    }

    /// <summary>
    /// Check resolution of tendency tones (leading tone, chordal seventh).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VoiceLeadingViolation CheckResolutions(Voicing from, Voicing to, int keyRoot, int seventhPc)
    {
        var violations = VoiceLeadingViolation.None;
        var leadingTone = (keyRoot + 11) % 12; // 7th scale degree

        // seventhPc is the chordal seventh of the 'from' chord (or -1 if it is not a seventh
        // chord), precomputed by the caller so it can be reused across many 'to' candidates.
        for (var v = 0; v < 4; v++)
        {
            var voice = (VoicePart)v;
            var fromPitch = from[voice];
            var toPitch = to[voice];
            var fromPitchClass = fromPitch % 12;

            // Leading tone should resolve up by step
            if (fromPitchClass == leadingTone)
            {
                var resolution = toPitch - fromPitch;
                // Should go up by 1 semitone (to tonic)
                if (resolution != 1 && resolution != -11) // Allow octave displacement
                {
                    // Only flag if it's an outer voice or if it doesn't resolve at all
                    if (voice == VoicePart.Soprano || voice == VoicePart.Bass || Math.Abs(resolution) > 2)
                    {
                        violations |= VoiceLeadingViolation.UnresolvedLeadingTone;
                    }
                }
            }

            // Chordal seventh should resolve down by step (or be held as a suspension)
            if (seventhPc >= 0 && fromPitchClass == seventhPc)
            {
                var resolution = toPitch - fromPitch;
                var resolvesDownByStep = resolution is -1 or -2;
                var held = resolution == 0;
                if (!resolvesDownByStep && !held)
                {
                    violations |= VoiceLeadingViolation.UnresolvedSeventh;
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Returns the pitch class of the chordal seventh of the voicing's chord,
    /// or -1 if the voicing is not recognized as a seventh chord. Exposed for callers that
    /// reuse it across many transitions from the same source voicing (see the
    /// <see cref="Check(Voicing, Voicing, int, int)"/> overload).
    /// </summary>
    internal static int GetChordalSeventhPitchClass(Voicing voicing)
    {
        Span<int> pitches = [voicing.Bass, voicing.Tenor, voicing.Alto, voicing.Soprano];
        var info = ChordAnalyzer.Identify(pitches);

        var seventhInterval = info.Quality switch
        {
            ChordQuality.Major7 or ChordQuality.MinorMajor7 => 11,
            ChordQuality.Dominant7 or ChordQuality.Minor7 or ChordQuality.HalfDim7
                or ChordQuality.Augmented7 or ChordQuality.Dominant7Flat5 => 10,
            ChordQuality.Diminished7 => 9,
            _ => -1
        };

        return seventhInterval < 0 ? -1 : (info.RootPitchClass + seventhInterval) % 12;
    }

    /// <summary>
    /// Check for problematic doublings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VoiceLeadingViolation CheckDoubling(Voicing voicing, int keyRoot)
    {
        var leadingTone = (keyRoot + 11) % 12;

        // Count pitch classes
        Span<int> counts = stackalloc int[12];
        counts[voicing.Bass % 12]++;
        counts[voicing.Tenor % 12]++;
        counts[voicing.Alto % 12]++;
        counts[voicing.Soprano % 12]++;

        return counts[leadingTone] switch
        {
            // Doubled leading tone is bad
            > 1 => VoiceLeadingViolation.DoubledLeadingTone,
            _ => VoiceLeadingViolation.None
        };
    }

    /// <summary>
    /// Calculate interval class (0-11) between two pitches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IntervalClass(int pitch1, int pitch2)
    {
        return (((pitch2 - pitch1) % 12) + 12) % 12;
    }

    /// <summary>
    /// Calculate total penalty from violations bitmask.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CalculatePenalty(VoiceLeadingViolation violations)
    {
        if (violations == VoiceLeadingViolation.None)
            return 0f;

        var penalty = 0f;
        var bits = (uint)violations;

        while (bits != 0)
        {
            var bit = BitOperations.TrailingZeroCount(bits);
            penalty += ViolationPenalties[bit];
            bits &= bits - 1; // Clear lowest bit
        }

        return penalty;
    }

    /// <summary>
    /// Score the smoothness of voice leading (lower = smoother).
    /// Measures total melodic motion across all voices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScoreSmoothness(Voicing from, Voicing to)
    {
        var totalMotion = 0;

        totalMotion += Math.Abs(to.Bass - from.Bass);
        totalMotion += Math.Abs(to.Tenor - from.Tenor);
        totalMotion += Math.Abs(to.Alto - from.Alto);
        totalMotion += Math.Abs(to.Soprano - from.Soprano);

        return totalMotion;
    }

    /// <summary>
    /// Combined score for voice leading quality (higher = better).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Score(Voicing from, Voicing to, int keyRoot = 0)
    {
        var check = Check(from, to, keyRoot);
        var smoothness = ScoreSmoothness(from, to);

        // Start with high score, subtract penalties
        // Smoothness contributes less than violations
        return 1000f - check.Penalty - (smoothness * 2f);
    }
}

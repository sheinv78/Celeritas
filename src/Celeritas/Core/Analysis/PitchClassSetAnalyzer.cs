// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Result of pitch-class set analysis: normal order, prime form, and interval vector (Forte set theory).
/// </summary>
/// <param name="Mask">12-bit pitch-class membership mask (bit <c>pc</c> set = that pitch class is present).</param>
/// <param name="Cardinality">Number of distinct pitch classes in the set.</param>
/// <param name="PitchClasses">Distinct pitch classes (0-11), ascending.</param>
/// <param name="NormalOrder">The set's normal order.</param>
/// <param name="PrimeForm">Prime form, transposed to begin on 0.</param>
/// <param name="IntervalVector">Interval-class vector &lt;ic1..ic6&gt;.</param>
public readonly record struct PitchClassSetAnalysisResult(
    ushort Mask,
    int Cardinality,
    int[] PitchClasses,
    int[] NormalOrder,
    int[] PrimeForm,
    int[] IntervalVector)
{
    /// <summary>Pitch classes formatted as <c>{a,b,c}</c>.</summary>
    public string PitchClassesText => "{" + string.Join(",", PitchClasses) + "}";

    /// <summary>Normal order formatted as <c>{a,b,c}</c>.</summary>
    public string NormalOrderText => "{" + string.Join(",", NormalOrder) + "}";

    /// <summary>Prime form formatted as <c>{a,b,c}</c>.</summary>
    public string PrimeFormText => "{" + string.Join(",", PrimeForm) + "}";

    /// <summary>Interval vector formatted as <c>&lt;a,b,c,d,e,f&gt;</c>.</summary>
    public string IntervalVectorText => "<" + string.Join(",", IntervalVector) + ">";
}

/// <summary>
/// Pitch-class set (PCS) analysis for atonal / post-tonal music.
/// Provides normal order, prime form, and interval vector.
/// </summary>
public static class PitchClassSetAnalyzer
{
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PitchClassSetAnalysisResult Analyze(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Analyze(buffer.PitchesReadOnly);
    }

    /// <summary>
    /// Analyze a pitch-class set from raw pitches (folded to pitch classes 0-11, deduplicated).
    /// </summary>
    public static PitchClassSetAnalysisResult Analyze(ReadOnlySpan<int> pitches)
    {
        var mask = ChordAnalyzer.GetMask(pitches);
        var pitchClasses = MaskToPitchClasses(mask);
        var cardinality = pitchClasses.Length;

        var normalOrder = GetNormalOrder(pitchClasses);
        var primeForm = GetPrimeForm(pitchClasses);
        var intervalVector = GetIntervalVector(pitchClasses);

        return new PitchClassSetAnalysisResult(mask, cardinality, pitchClasses, normalOrder, primeForm, intervalVector);
    }

    /// <summary>
    /// Expand a 12-bit pitch-class mask into an ascending array of pitch classes (0-11).
    /// </summary>
    public static int[] MaskToPitchClasses(ushort mask)
    {
        if (mask == 0)
        {
            return [];
        }

        var count = 0;
        for (var pc = 0; pc < 12; pc++)
        {
            if (((mask >> pc) & 1) != 0)
            {
                count++;
            }
        }

        var result = new int[count];
        var idx = 0;
        for (var pc = 0; pc < 12; pc++)
        {
            if (((mask >> pc) & 1) != 0)
            {
                result[idx++] = pc;
            }
        }

        return result;
    }

    /// <summary>
    /// Reduce arbitrary input to the pitch-class set the algorithms below assume: folded to
    /// [0, 12), distinct, ascending.
    /// </summary>
    /// <remarks>
    /// The algorithms in this file were written against <see cref="MaskToPitchClasses"/>'s output
    /// and say so in their comments, but nothing enforced it, and they are public: callers pass
    /// raw MIDI pitches, unsorted sets, duplicates. Nothing rejected that — the rotation search
    /// simply ran on values that were not pitch classes and returned an answer of the right shape.
    /// GetNormalOrder([4,0,7]) came back as [4,0,7], and GetPrimeForm([60,64,67]) disagreed with
    /// GetPrimeForm([48,52,55]) — the same chord an octave apart.
    ///
    /// Routing through GetMask reuses the one implementation of this fold that is already
    /// property-tested, rather than adding another hand-written copy.
    /// </remarks>
    private static int[] ToPitchClassSet(int[] pitches) =>
        MaskToPitchClasses(ChordAnalyzer.GetMask(pitches));

    /// <exception cref="ArgumentNullException"><paramref name="pitchClasses"/> is <see langword="null"/>.</exception>
    public static int[] GetNormalOrder(int[] pitchClasses)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        pitchClasses = ToPitchClassSet(pitchClasses);

        if (pitchClasses.Length <= 1)
        {
            return [.. pitchClasses];
        }

        var n = pitchClasses.Length;

        int[]? best = null;
        int[]? bestExtended = null;
        var bestSpan = int.MaxValue;

        for (var start = 0; start < n; start++)
        {
            var extended = new int[n];
            var basePc = pitchClasses[start];

            for (var k = 0; k < n; k++)
            {
                var pc = pitchClasses[(start + k) % n];
                if (pc < basePc)
                {
                    pc += 12;
                }

                extended[k] = pc;
            }

            var span = extended[n - 1] - extended[0];

            if (span < bestSpan)
            {
                bestSpan = span;
                bestExtended = extended;
                best = ExtendedToPitchClasses(extended);
                continue;
            }

            if (span == bestSpan && bestExtended != null)
            {
                if (IsMoreLeftPacked(extended, bestExtended))
                {
                    bestExtended = extended;
                    best = ExtendedToPitchClasses(extended);
                }
            }
        }

        return best ?? [.. pitchClasses];
    }

    /// <exception cref="ArgumentNullException"><paramref name="pitchClasses"/> is <see langword="null"/>.</exception>
    public static int[] GetPrimeForm(int[] pitchClasses)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        pitchClasses = ToPitchClassSet(pitchClasses);

        if (pitchClasses.Length == 0)
        {
            return [];
        }

        // Prime form is transposed to begin on 0, so a single pitch class is [0] — not the pitch
        // class itself. Returning `pitchClasses` here made GetPrimeForm([1]) == [1], which broke
        // both the "starts at 0" rule and transposition/inversion invariance (the whole point of a
        // prime form) for every one-note set. Property tests caught it; the example-based tests,
        // which all used triads, never did.
        if (pitchClasses.Length == 1)
        {
            return [0];
        }

        var normal = GetNormalOrder(pitchClasses);
        var primeA = TransposeToZero(normal);

        var inverted = Invert(pitchClasses);
        var normalInv = GetNormalOrder(inverted);
        var primeB = TransposeToZero(normalInv);

        return CompareLex(primeA, primeB) <= 0 ? primeA : primeB;
    }

    /// <exception cref="ArgumentNullException"><paramref name="pitchClasses"/> is <see langword="null"/>.</exception>
    public static int[] GetIntervalVector(int[] pitchClasses)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        // Duplicates would each contribute their own intervals, so the same set written twice
        // over would score as denser interval content than it has.
        pitchClasses = ToPitchClassSet(pitchClasses);

        // Interval vector counts unordered pitch-class intervals 1..6.
        // Output order: <ic1, ic2, ic3, ic4, ic5, ic6>
        var n = pitchClasses.Length;
        var iv = new int[6];

        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var interval = pitchClasses[j] - pitchClasses[i];
                if (interval < 0)
                {
                    interval += 12;
                }

                interval %= 12;
                if (interval == 0)
                {
                    continue;
                }

                var ic = Math.Min(interval, 12 - interval);
                if (ic is >= 1 and <= 6)
                {
                    iv[ic - 1]++;
                }
            }
        }

        return iv;
    }

    /// <exception cref="ArgumentNullException"><paramref name="pitchClasses"/> is <see langword="null"/>.</exception>
    public static int[] Transpose(int[] pitchClasses, int semitones)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        if (pitchClasses.Length == 0)
        {
            return [];
        }

        semitones %= 12;
        if (semitones < 0)
        {
            semitones += 12;
        }

        var result = new int[pitchClasses.Length];
        for (var i = 0; i < pitchClasses.Length; i++)
        {
            // Fold the input element too, as Invert does: `%` keeps the sign, so a negative pitch
            // class came out negative — Transpose([-1], 0) returned [-1], not a pitch class at all.
            var pc = PitchMath.Fold(pitchClasses[i]);
            result[i] = (pc + semitones) % 12;
        }

        Array.Sort(result);
        return result;
    }

    /// <exception cref="ArgumentNullException"><paramref name="pitchClasses"/> is <see langword="null"/>.</exception>
    public static int[] Invert(int[] pitchClasses)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        if (pitchClasses.Length == 0)
        {
            return [];
        }

        var result = new int[pitchClasses.Length];
        for (var i = 0; i < pitchClasses.Length; i++)
        {
            var pc = PitchMath.Fold(pitchClasses[i]);
            result[i] = (12 - pc) % 12;
        }

        Array.Sort(result);
        return result;
    }

    /// <summary>
    /// Gets the complement of a pitch class set (all pitch classes NOT in the set).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="pitchClasses"/> is <see langword="null"/>.</exception>
    public static int[] Complement(int[] pitchClasses)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        if (pitchClasses.Length == 0)
        {
            return [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
        }

        var mask = 0;
        foreach (var pc in pitchClasses)
        {
            mask |= 1 << (pc % 12);
        }

        var complementMask = ~mask & 0xFFF;
        return MaskToPitchClasses((ushort)complementMask);
    }

    /// <summary>
    /// Calculates similarity between two pitch class sets using interval vector comparison.
    /// Returns a value from 0.0 (completely different) to 1.0 (identical interval content).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="set1"/> or <paramref name="set2"/> is <see langword="null"/>.</exception>
    public static double Similarity(int[] set1, int[] set2)
    {
        ArgumentNullException.ThrowIfNull(set1);
        ArgumentNullException.ThrowIfNull(set2);

        var iv1 = GetIntervalVector(set1);
        var iv2 = GetIntervalVector(set2);

        // Calculate similarity using cosine similarity of interval vectors
        var dotProduct = 0;
        var mag1 = 0;
        var mag2 = 0;

        for (var i = 0; i < 6; i++)
        {
            dotProduct += iv1[i] * iv2[i];
            mag1 += iv1[i] * iv1[i];
            mag2 += iv2[i] * iv2[i];
        }

        // Cosine similarity is undefined for a zero vector, and a set of fewer than two pitch
        // classes has one: it contains no intervals at all. Collapsing both cases to 0.0 answered
        // "completely different" for two sets with identical interval content — Similarity([0],
        // [0]) said a set was completely different from itself. Empty content matching empty
        // content is a match; empty against non-empty is not.
        if (mag1 == 0 && mag2 == 0)
        {
            return 1.0;
        }

        if (mag1 == 0 || mag2 == 0)
        {
            return 0.0;
        }

        return dotProduct / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }

    private static int[] ExtendedToPitchClasses(int[] extended)
    {
        var n = extended.Length;
        var result = new int[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = extended[i] % 12;
        }

        return result;
    }

    private static bool IsMoreLeftPacked(int[] candidate, int[] best)
    {
        // Tie-breaker for normal order: compare distances from first element, from right to left.
        // The one with smaller distance earlier is more left-packed.
        var n = candidate.Length;
        for (var i = n - 1; i >= 1; i--)
        {
            var dc = candidate[i] - candidate[0];
            var db = best[i] - best[0];
            if (dc != db)
            {
                return dc < db;
            }
        }

        return false;
    }

    private static int[] TransposeToZero(int[] pcsInNormalOrder)
    {
        var n = pcsInNormalOrder.Length;
        if (n == 0)
        {
            return [];
        }

        var first = pcsInNormalOrder[0];
        var result = new int[n];
        for (var i = 0; i < n; i++)
        {
            var v = pcsInNormalOrder[i] - first;
            v %= 12;
            if (v < 0)
            {
                v += 12;
            }

            result[i] = v;
        }

        return result;
    }

    private static int CompareLex(int[] a, int[] b)
    {
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            if (a[i] != b[i])
            {
                return a[i].CompareTo(b[i]);
            }
        }
        return a.Length.CompareTo(b.Length);
    }
}

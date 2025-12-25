// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core.Analysis;

public readonly record struct PitchClassSetAnalysisResult(
    ushort Mask,
    int Cardinality,
    int[] PitchClasses,
    int[] NormalOrder,
    int[] PrimeForm,
    int[] IntervalVector)
{
    public string PitchClassesText => "{" + string.Join(",", PitchClasses) + "}";
    public string NormalOrderText => "{" + string.Join(",", NormalOrder) + "}";
    public string PrimeFormText => "{" + string.Join(",", PrimeForm) + "}";
    public string IntervalVectorText => "<" + string.Join(",", IntervalVector) + ">";
}

/// <summary>
/// Pitch-class set (PCS) analysis for atonal / post-tonal music.
/// Provides normal order, prime form, and interval vector.
/// </summary>
public static class PitchClassSetAnalyzer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PitchClassSetAnalysisResult Analyze(NoteBuffer buffer) => Analyze(buffer.PitchesReadOnly);

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

    public static int[] MaskToPitchClasses(ushort mask)
    {
        if (mask == 0)
            return [];

        var count = 0;
        for (var pc = 0; pc < 12; pc++)
            if (((mask >> pc) & 1) != 0)
                count++;

        var result = new int[count];
        var idx = 0;
        for (var pc = 0; pc < 12; pc++)
            if (((mask >> pc) & 1) != 0)
                result[idx++] = pc;

        return result;
    }

    public static int[] GetNormalOrder(int[] pitchClasses)
    {
        if (pitchClasses.Length <= 1)
            return pitchClasses.ToArray();

        // Input is already sorted ascending (MaskToPitchClasses ensures this).
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
                    pc += 12;
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

        return best ?? pitchClasses.ToArray();
    }

    public static int[] GetPrimeForm(int[] pitchClasses)
    {
        if (pitchClasses.Length <= 1)
            return pitchClasses.ToArray();

        var normal = GetNormalOrder(pitchClasses);
        var primeA = TransposeToZero(normal);

        var inverted = Invert(pitchClasses);
        var normalInv = GetNormalOrder(inverted);
        var primeB = TransposeToZero(normalInv);

        return CompareLex(primeA, primeB) <= 0 ? primeA : primeB;
    }

    public static int[] GetIntervalVector(int[] pitchClasses)
    {
        // Interval vector counts unordered pitch-class intervals 1..6.
        // Output order: <ic1, ic2, ic3, ic4, ic5, ic6>
        var n = pitchClasses.Length;
        var iv = new int[6];

        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var interval = pitchClasses[j] - pitchClasses[i];
                if (interval < 0) interval += 12;

                interval %= 12;
                if (interval == 0) continue;

                var ic = Math.Min(interval, 12 - interval);
                if (ic is >= 1 and <= 6)
                    iv[ic - 1]++;
            }
        }

        return iv;
    }

    public static int[] Transpose(int[] pitchClasses, int semitones)
    {
        if (pitchClasses.Length == 0)
            return [];

        semitones %= 12;
        if (semitones < 0) semitones += 12;

        var result = new int[pitchClasses.Length];
        for (var i = 0; i < pitchClasses.Length; i++)
            result[i] = (pitchClasses[i] + semitones) % 12;

        Array.Sort(result);
        return result;
    }

    public static int[] Invert(int[] pitchClasses)
    {
        if (pitchClasses.Length == 0)
            return [];

        var result = new int[pitchClasses.Length];
        for (var i = 0; i < pitchClasses.Length; i++)
        {
            var pc = pitchClasses[i] % 12;
            if (pc < 0) pc += 12;
            result[i] = (12 - pc) % 12;
        }

        Array.Sort(result);
        return result;
    }

    private static int[] ExtendedToPitchClasses(int[] extended)
    {
        var n = extended.Length;
        var result = new int[n];
        for (var i = 0; i < n; i++)
            result[i] = extended[i] % 12;
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
                return dc < db;
        }

        return false;
    }

    private static int[] TransposeToZero(int[] pcsInNormalOrder)
    {
        var n = pcsInNormalOrder.Length;
        if (n == 0) return [];

        var first = pcsInNormalOrder[0];
        var result = new int[n];
        for (var i = 0; i < n; i++)
        {
            var v = pcsInNormalOrder[i] - first;
            v %= 12;
            if (v < 0) v += 12;
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
                return a[i].CompareTo(b[i]);
        }
        return a.Length.CompareTo(b.Length);
    }
}

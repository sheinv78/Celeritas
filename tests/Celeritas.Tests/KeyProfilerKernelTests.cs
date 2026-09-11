// Copyright (c) 2025 Vladimir V. Shein

using System.Runtime.Intrinsics.X86;
using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Simd;

namespace Celeritas.Tests;

/// <summary>
/// Key detection correlates a pitch-class distribution against 24 profiles, and it does that
/// through one of three kernels chosen by what the CPU offers. Only one of them runs on any
/// given machine, so a SIMD kernel that drifted from the scalar reference would be invisible
/// until someone ran the library on the hardware that selects it. These run every kernel the
/// host can execute and require them to agree.
/// </summary>
public class KeyProfilerKernelTests
{
    /// <summary>Distributions chosen to exercise the arithmetic, not just to be plausible music.</summary>
    public static TheoryData<float[]> Distributions()
    {
        var data = new TheoryData<float[]>();
        data.Add([1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0]);                     // a C major triad
        data.Add([4.5f, 0, 2, 0, 3.5f, 1, 0, 4, 0, 1.5f, 0, 2]);            // a weighted major-ish set
        data.Add([1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);                     // every pitch class equally
        data.Add(new float[12]);                                            // silence
        data.Add([100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);                   // one very loud note
        data.Add([0.001f, 0.002f, 0.003f, 0.004f, 0.005f, 0.006f,           // tiny values
                  0.007f, 0.008f, 0.009f, 0.01f, 0.011f, 0.012f]);
        return data;
    }

    private static float[] Run(Action<ReadOnlySpan<float>, Span<float>> kernel, float[] distribution)
    {
        var correlations = new float[24];
        kernel(distribution, correlations);
        return correlations;
    }

    /// <summary>
    /// The kernels add the same twelve products in different orders, so they agree to within
    /// float rounding rather than bit-for-bit. A drifted kernel misses by orders of magnitude
    /// more than this.
    /// </summary>
    private static void AssertAgrees(float reference, float actual, int key, string kernel)
    {
        var tolerance = 1e-4f * Math.Max(1f, Math.Abs(reference));

        Assert.True(
            Math.Abs(reference - actual) <= tolerance,
            $"{kernel} key {key}: {actual} differs from the scalar {reference} by more than {tolerance}");
    }

    [Theory]
    [MemberData(nameof(Distributions))]
    public void EveryKernelTheHostCanRun_AgreesWithTheScalarReference(float[] distribution)
    {
        var scalar = Run(KeyProfiler.ComputeCorrelationsScalar, distribution);

        Assert.Equal(24, scalar.Length);

        if (Avx2.IsSupported)
        {
            var avx2 = Run(KeyProfiler.ComputeCorrelationsAvx2, distribution);

            for (var key = 0; key < 24; key++)
                AssertAgrees(scalar[key], avx2[key], key, "AVX2");
        }

        if (Avx512F.IsSupported)
        {
            var avx512 = Run(KeyProfiler.ComputeCorrelationsAvx512, distribution);

            for (var key = 0; key < 24; key++)
                AssertAgrees(scalar[key], avx512[key], key, "AVX-512");
        }
    }

    [Fact]
    public void AtLeastOneKernelRanOnThisMachine()
    {
        // Guards the test above from passing vacuously: the scalar path always runs, and the
        // vector paths run wherever the host offers them.
        Assert.True(
            Avx2.IsSupported || Avx512F.IsSupported || SimdInfo.GetBest() == SimdInstructionSet.None
            || SimdInfo.GetBest() == SimdInstructionSet.Sse2 || SimdInfo.GetBest() == SimdInstructionSet.Neon
            || SimdInfo.GetBest() == SimdInstructionSet.WasmSimd,
            $"unexpected instruction set: {SimdInfo.GetBest()}");
    }

    [Fact]
    public void TheKeyTheDetectorReports_IsTheStrongestCorrelationItReports()
    {
        // Whichever kernel the host selected, the answer has to be the largest of the numbers
        // it produced — not a different key with the correlations attached for show.
        var detected = KeyProfiler.DetectFromPitches([60, 60, 62, 64, 64, 65, 67, 67, 67, 69, 71]);

        Assert.Equal(24, detected.AllCorrelations.Length);

        var strongest = detected.AllCorrelations
            .OrderByDescending(c => c.Correlation)
            .First();

        Assert.Equal(strongest.Key.Root, detected.Key.Root);
        Assert.Equal(strongest.Key.IsMajor, detected.Key.IsMajor);
        Assert.InRange(detected.Confidence, 0f, 1f);
    }

    [Fact]
    public void EveryKeyIsCorrelated_StrongestFirst()
    {
        var detected = KeyProfiler.DetectFromPitches([60, 64, 67]);

        Assert.Equal(24, detected.AllCorrelations.Length);
        Assert.Equal(12, detected.AllCorrelations.Count(c => c.Key.IsMajor));
        Assert.Equal(12, detected.AllCorrelations.Count(c => !c.Key.IsMajor));
        Assert.Equal(
            Enumerable.Range(0, 12),
            detected.AllCorrelations.Where(c => c.Key.IsMajor).Select(c => (int)c.Key.Root).Order());
        // Descending to within the kernels' noise: a run of keys the correlation cannot tell
        // apart is ordered by key index, which may put a value a few 1e-7 smaller first.
        for (var i = 1; i < detected.AllCorrelations.Length; i++)
        {
            Assert.True(
                detected.AllCorrelations[i].Correlation <= detected.AllCorrelations[i - 1].Correlation + 1e-5f,
                $"{detected.AllCorrelations[i]} follows {detected.AllCorrelations[i - 1]}");
        }
    }

    [Fact]
    public void SilenceCorrelatesWithNothing()
    {
        var scalar = Run(KeyProfiler.ComputeCorrelationsScalar, new float[12]);

        Assert.All(scalar, c => Assert.Equal(0f, c));
    }

    [Fact]
    public void EveryPitchClassEqually_CorrelatesTheSameWithMajorAndMinor()
    {
        // A flat distribution has no key in it: every major profile sums the same weight, and
        // so does every minor one.
        var flat = Enumerable.Repeat(1f, 12).ToArray();

        var scalar = Run(KeyProfiler.ComputeCorrelationsScalar, flat);

        for (var root = 1; root < 12; root++)
        {
            Assert.Equal(scalar[0], scalar[root], 4);
            Assert.Equal(scalar[12], scalar[12 + root], 4);
        }
    }

    [Fact]
    public void TheCorrelationIsLinearInTheDistribution()
    {
        var single = new float[] { 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0 };
        var doubled = single.Select(v => v * 2).ToArray();

        var a = Run(KeyProfiler.ComputeCorrelationsScalar, single);
        var b = Run(KeyProfiler.ComputeCorrelationsScalar, doubled);

        for (var key = 0; key < 24; key++)
            Assert.Equal(a[key] * 2, b[key], 4);
    }
    // ---------- the answer does not depend on which kernel ran ----------

    public static TheoryData<string, int[]> SymmetricMusic =>
        new()
        {
            { "7b5, which maps onto itself a tritone away", new[] { 60, 64, 66, 70 } },
            { "an augmented triad, three ways", new[] { 60, 64, 68 } },
            { "a diminished seventh, four ways", new[] { 60, 63, 66, 69 } },
            { "a whole-tone scale, six ways", new[] { 60, 62, 64, 66, 68, 70 } },
            { "every note there is", new[] { 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71 } },
        };

    [Theory]
    [MemberData(nameof(SymmetricMusic))]
    public void MusicSymmetricUnderTransposition_PicksTheLowestOfTheKeysItCannotChooseBetween(
        string what, int[] pitches)
    {
        // Such music gives two or more keys the same correlation exactly. The kernels sum in
        // different orders, so "exactly" arrives as 0.28461015 against 0.28461018, and comparing
        // those directly let the CPU decide: the natively compiled bindings reported C major for
        // a chord this build called F# major. Anything inside the kernels' own noise is now the
        // same score, so the lowest key index wins wherever it runs.
        var detected = KeyProfiler.DetectFromPitches(pitches);
        var best = detected.AllCorrelations.Max(c => c.Correlation);

        var indistinguishable = detected.AllCorrelations
            .Where(c => best - c.Correlation <= 1e-5f)
            .Select(c => c.Key.Root + (c.Key.IsMajor ? 0 : 12))
            .ToArray();

        Assert.True(indistinguishable.Length > 1, $"{what} was expected to tie");
        Assert.Equal(
            indistinguishable.Min(),
            detected.Key.Root + (detected.Key.IsMajor ? 0 : 12));
    }

    [Theory]
    [MemberData(nameof(SymmetricMusic))]
    public void TheCorrelationListLeadsWithTheKeyThatWasChosen(string what, int[] pitches)
    {
        // Sorting on the raw correlation put the noisily-larger member of a tie first, so
        // TopKeys(1) named a different key from Key for exactly this music.
        var detected = KeyProfiler.DetectFromPitches(pitches);

        Assert.Equal(detected.Key, detected.TopKeys(1).First().Key);
        Assert.Equal(detected.Key, detected.AllCorrelations[0].Key);
        Assert.False(string.IsNullOrWhiteSpace(what));
    }

    [Fact]
    public void MusicThatDoesDecideAKey_IsUnaffectedByTheMargin()
    {
        // A clear detection separates its winner by 0.1 to 0.35, four orders of magnitude above
        // the kernels' noise, so the margin must not have blunted it.
        var detected = KeyProfiler.DetectFromPitches([60, 62, 64, 65, 67, 69, 71]);

        Assert.Equal(new KeySignature(0, true), detected.Key);
        Assert.True(detected.Confidence > 0.05f, $"confidence collapsed to {detected.Confidence}");
    }

    // ---------- the order of the list does not depend on which kernel ran ----------
    //
    // The winner was already settled within the kernels' noise, but the list under it was
    // sorted on the raw correlation, so a tie stood in whichever order the kernel's rounding
    // put it. Over the 4095 pitch-class subsets the AVX kernels and the scalar one ordered 358
    // lists differently, 111 of them within the top five: C and F# alone tie C minor with
    // F# minor, and AVX-512 listed C minor third where the scalar kernel listed F# minor.

    /// <summary>Every kernel this host can execute, the scalar reference always among them.</summary>
    private static IEnumerable<(string Name, KeyProfiler.CorrelationComputer Kernel)> KernelsTheHostCanRun()
    {
        yield return ("scalar", KeyProfiler.ComputeCorrelationsScalar);
        if (Avx2.IsSupported)
            yield return ("AVX2", KeyProfiler.ComputeCorrelationsAvx2);
        if (Avx512F.IsSupported)
            yield return ("AVX-512", KeyProfiler.ComputeCorrelationsAvx512);
    }

    /// <summary>Every non-empty set of pitch classes, each sounded once.</summary>
    private static IEnumerable<(int Subset, float[] Distribution)> EveryPitchClassSubset()
    {
        for (var subset = 1; subset < 4096; subset++)
        {
            var distribution = new float[12];
            for (var pc = 0; pc < 12; pc++)
                distribution[pc] = (subset & (1 << pc)) != 0 ? 1f : 0f;
            yield return (subset, distribution);
        }
    }

    private static int KeyIndex(KeySignature key) => key.IsMajor ? key.Root : 12 + key.Root;

    private static string Order(KeyDetectionResult detected) =>
        string.Join(",", detected.AllCorrelations.Select(c => KeyIndex(c.Key)));

    [Fact]
    public void TheCorrelationList_IsInTheSameOrder_WhicheverKernelRan()
    {
        var kernels = KernelsTheHostCanRun().ToArray();
        var disagreements = new List<string>();

        foreach (var (subset, distribution) in EveryPitchClassSubset())
        {
            var reference = KeyProfiler.Detect(distribution, kernels[0].Kernel);
            var referenceOrder = Order(reference);

            foreach (var (name, kernel) in kernels.Skip(1))
            {
                var detected = KeyProfiler.Detect(distribution, kernel);

                if (detected.Key != reference.Key || Order(detected) != referenceOrder)
                    disagreements.Add($"subset {subset:X3}: {name} [{Order(detected)}] vs {kernels[0].Name} [{referenceOrder}]");
            }
        }

        Assert.True(
            disagreements.Count == 0,
            $"{disagreements.Count} of 4095 subsets are listed in a different order by different kernels:\n"
            + string.Join("\n", disagreements.Take(10)));
    }

    [Fact]
    public void ATieInTheCorrelationList_IsListedInKeyOrder_WhicheverKernelRan()
    {
        // Independent of the test above, which would pass vacuously on a host with one kernel:
        // this states the rule itself. Every run of keys within the kernels' noise of the run's
        // strongest is in key order, majors C..B before minors C..B.
        foreach (var (name, kernel) in KernelsTheHostCanRun())
        {
            foreach (var (subset, distribution) in EveryPitchClassSubset())
            {
                var list = KeyProfiler.Detect(distribution, kernel).AllCorrelations;

                var runStart = 0;
                for (var i = 1; i < list.Length; i++)
                {
                    if (list[runStart].Correlation - list[i].Correlation > 1e-5f)
                    {
                        runStart = i;
                        continue;
                    }

                    Assert.True(
                        KeyIndex(list[i - 1].Key) < KeyIndex(list[i].Key),
                        $"{name}, subset {subset:X3}: {list[i - 1]} is listed before {list[i]} though they tie");
                }
            }
        }
    }

    [Fact]
    public void TheAugmentedTriad_ListsItsTiedKeysInKeyOrder()
    {
        // C, E and G# tie three majors and three minors. Sorted on the raw value the top five
        // ended in F minor and A minor on an AVX machine but A minor and C# minor on a scalar
        // one; now it is the same five, in key order, on every machine.
        var detected = KeyProfiler.DetectFromPitches([60, 64, 68]);

        Assert.Equal(
            [
                new KeySignature(0, true), new KeySignature(4, true), new KeySignature(8, true),
                new KeySignature(1, false), new KeySignature(5, false),
            ],
            detected.TopKeys(5).Select(c => c.Key));
    }

    [Fact]
    public void TheTritone_ListsCMinorBeforeFSharpMinor_OnTheScalarKernel()
    {
        // The scalar kernel rounds F# minor a few 1e-7 above C minor for these two notes, and
        // the raw sort listed it first; the AVX kernels round the other way. Pinned on the one
        // kernel every host can run, so this is not vacuous anywhere.
        var detected = KeyProfiler.Detect(
            [1f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f],
            KeyProfiler.ComputeCorrelationsScalar);

        Assert.Equal(
            [
                new KeySignature(0, true), new KeySignature(6, true),
                new KeySignature(0, false), new KeySignature(6, false),
            ],
            detected.TopKeys(4).Select(c => c.Key));
    }
}

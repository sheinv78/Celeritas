using Celeritas.Core.Analysis;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Property-based tests (CsCheck) for the invariants pitch-class set theory guarantees. These are
/// the theorems the analyzer must obey, checked against every set CsCheck can find rather than the
/// handful of examples in the issues that motivated them (#23).
/// </summary>
/// <remarks>
/// Prime form and interval vector are, by definition, invariant under transposition and inversion
/// — a set and all its transpositions and its inversion share one prime form and one interval
/// vector. If normalization is wrong for some input, that equality is where it shows.
/// </remarks>
public class PropertyPitchClassSetTests
{
    // Legitimate pitch classes: 0..11, one to eight of them. Duplicates are allowed — the analyzer
    // reduces to a set — so the generator does not deduplicate; that is itself under test.
    private static readonly Gen<int[]> PitchClasses = Gen.Int[0, 11].Array[1, 8];

    private static readonly Gen<int> Semitones = Gen.Int[-24, 24];

    [Fact]
    public void PrimeForm_IsInvariantUnderTransposition()
    {
        (from set in PitchClasses from n in Semitones select (set, n)).Sample(t =>
        {
            var transposed = PitchClassSetAnalyzer.Transpose(t.set, t.n);
            Assert.Equal(
                PitchClassSetAnalyzer.GetPrimeForm(t.set),
                PitchClassSetAnalyzer.GetPrimeForm(transposed));
        });
    }

    [Fact]
    public void PrimeForm_IsInvariantUnderInversion()
    {
        PitchClasses.Sample(set =>
        {
            var inverted = PitchClassSetAnalyzer.Invert(set);
            Assert.Equal(
                PitchClassSetAnalyzer.GetPrimeForm(set),
                PitchClassSetAnalyzer.GetPrimeForm(inverted));
        });
    }

    [Fact]
    public void IntervalVector_IsInvariantUnderTransposition()
    {
        (from set in PitchClasses from n in Semitones select (set, n)).Sample(t =>
        {
            var transposed = PitchClassSetAnalyzer.Transpose(t.set, t.n);
            Assert.Equal(
                PitchClassSetAnalyzer.GetIntervalVector(t.set),
                PitchClassSetAnalyzer.GetIntervalVector(transposed));
        });
    }

    [Fact]
    public void IntervalVector_IsInvariantUnderInversion()
    {
        PitchClasses.Sample(set =>
        {
            var inverted = PitchClassSetAnalyzer.Invert(set);
            Assert.Equal(
                PitchClassSetAnalyzer.GetIntervalVector(set),
                PitchClassSetAnalyzer.GetIntervalVector(inverted));
        });
    }

    [Fact]
    public void Inversion_IsAnInvolution()
    {
        PitchClasses.Sample(set =>
        {
            var back = PitchClassSetAnalyzer.Invert(PitchClassSetAnalyzer.Invert(set));
            // Inverting twice returns the original set (up to normalization).
            Assert.Equal(
                PitchClassSetAnalyzer.GetNormalOrder(set),
                PitchClassSetAnalyzer.GetNormalOrder(back));
        });
    }

    [Fact]
    public void Transposition_IsAdditiveModTwelve()
    {
        (from set in PitchClasses from a in Semitones from b in Semitones select (set, a, b)).Sample(t =>
        {
            var twice = PitchClassSetAnalyzer.Transpose(PitchClassSetAnalyzer.Transpose(t.set, t.a), t.b);
            var once = PitchClassSetAnalyzer.Transpose(t.set, t.a + t.b);
            // Transpose sorts and folds, so equal sets come out as equal arrays.
            Assert.Equal(
                PitchClassSetAnalyzer.GetNormalOrder(once),
                PitchClassSetAnalyzer.GetNormalOrder(twice));
        });
    }

    [Fact]
    public void TransposeAndInvert_AlwaysReturnValidPitchClasses()
    {
        // Includes negatives and out-of-octave values: the output must still be pitch classes in
        // [0, 12). Transpose used not to fold a negative input element — Transpose([-1], 0) came
        // back as [-1], not a pitch class — while Invert did fold. Both are checked here.
        var anyInts = Gen.Int[-50, 50].Array[1, 8];

        (from set in anyInts from n in Semitones select (set, n)).Sample(t =>
        {
            Assert.All(PitchClassSetAnalyzer.Transpose(t.set, t.n), pc => Assert.InRange(pc, 0, 11));
            Assert.All(PitchClassSetAnalyzer.Invert(t.set), pc => Assert.InRange(pc, 0, 11));
        });
    }

    [Fact]
    public void NormalOrder_IsIdempotent()
    {
        PitchClasses.Sample(set =>
        {
            var once = PitchClassSetAnalyzer.GetNormalOrder(set);
            var twice = PitchClassSetAnalyzer.GetNormalOrder(once);
            Assert.Equal(once, twice);
        });
    }

    [Fact]
    public void GetPrimeForm_StartsAtZero_AndIsAscending()
    {
        PitchClasses.Sample(set =>
        {
            var prime = PitchClassSetAnalyzer.GetPrimeForm(set);
            if (prime.Length == 0)
            {
                return;
            }

            Assert.Equal(0, prime[0]); // prime form is transposed to begin on 0
            for (var i = 1; i < prime.Length; i++)
            {
                Assert.True(prime[i] > prime[i - 1], $"prime form not strictly ascending: [{string.Join(",", prime)}]");
            }
        });
    }

    [Fact]
    public void IntervalVector_HasSixEntries_SummingToThePairCount()
    {
        PitchClasses.Sample(set =>
        {
            var distinct = set.Distinct().Count();
            var iv = PitchClassSetAnalyzer.GetIntervalVector(set);

            Assert.Equal(6, iv.Length);
            // Every unordered pair of distinct pitch classes contributes exactly one interval class.
            Assert.Equal(distinct * (distinct - 1) / 2, iv.Sum());
        });
    }
}

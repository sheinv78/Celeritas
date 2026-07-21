using System.Numerics;
using Celeritas.Core;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Property-based tests (CsCheck) for <see cref="ChordAnalyzer.GetMask(ReadOnlySpan{int})"/> invariants.
/// </summary>
public class PropertyChordMaskTests
{
    // Includes negatives; bounded so octave shifts (+/- 12*k) cannot overflow int.
    private static readonly Gen<int[]> Pitches = Gen.Int[-1_000, 1_000].Array[0, 32];

    private static int DistinctPitchClasses(int[] pitches)
    {
        var set = new HashSet<int>();
        foreach (var p in pitches)
        {
            set.Add(((p % 12) + 12) % 12);
        }

        return set.Count;
    }

    [Fact]
    public void Mask_PopcountEqualsDistinctPitchClasses()
    {
        Pitches.Sample(pitches =>
        {
            var mask = ChordAnalyzer.GetMask(pitches);
            Assert.Equal(DistinctPitchClasses(pitches), BitOperations.PopCount(mask));
        });
    }

    [Fact]
    public void Mask_InvariantToOctaveShift()
    {
        (from pitches in Pitches from k in Gen.Int[-8, 8] select (pitches, k))
            .Sample(t =>
            {
                var (pitches, k) = t;
                var shifted = new int[pitches.Length];
                for (var i = 0; i < pitches.Length; i++)
                {
                    shifted[i] = pitches[i] + (12 * k);
                }

                Assert.Equal(ChordAnalyzer.GetMask(pitches), ChordAnalyzer.GetMask(shifted));
            });
    }

    [Fact]
    public void Mask_InvariantToPermutation()
    {
        (from pitches in Pitches from seed in Gen.Int[0, int.MaxValue] select (pitches, seed))
            .Sample(t =>
            {
                var (pitches, seed) = t;
                var shuffled = (int[])pitches.Clone();
                // Fisher-Yates with a deterministic per-case seed.
                var rng = new Random(seed);
                for (var i = shuffled.Length - 1; i > 0; i--)
                {
                    var j = rng.Next(i + 1);
                    (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
                }

                Assert.Equal(ChordAnalyzer.GetMask(pitches), ChordAnalyzer.GetMask(shuffled));
            });
    }

    [Fact]
    public void Mask_NegativePitchMapsToModTwelve()
    {
        // A single negative pitch and its non-negative mod-12 representative share one mask bit.
        Gen.Int[-1_000, -1].Sample(p =>
        {
            var pc = ((p % 12) + 12) % 12;
            Assert.Equal(ChordAnalyzer.GetMask([pc]), ChordAnalyzer.GetMask([p]));
        });
    }
}

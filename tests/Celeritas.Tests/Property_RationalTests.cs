using Celeritas.Core;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Property-based tests (CsCheck) for <see cref="Rational"/> invariants over randomized inputs.
/// </summary>
public class PropertyRationalTests
{
    // Bounded so the exact 64-bit arithmetic in Rational never deliberately overflows.
    private static readonly Gen<Rational> RationalBig =
        from n in Gen.Long[-1_000_000, 1_000_000]
        from d in Gen.Long[-1_000_000, 1_000_000].Where(x => x != 0)
        select new Rational(n, d);

    // Smaller magnitudes for multi-step arithmetic chains (intermediate denominators stay in range).
    private static readonly Gen<Rational> RationalSmall =
        from n in Gen.Long[-1_000, 1_000]
        from d in Gen.Long[-1_000, 1_000].Where(x => x != 0)
        select new Rational(n, d);

    private static long Gcd(long a, long b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }

    [Fact]
    public void Construction_IsAlwaysNormalized()
    {
        RationalBig.Sample(r =>
        {
            Assert.True(r.Denominator > 0, "denominator must be positive");
            // gcd(|num|, den) == 1 for a fully reduced fraction (num==0 gives gcd(0,1)==1).
            Assert.Equal(1L, Gcd(r.Numerator, r.Denominator));
        });
    }

    [Fact]
    public void Addition_IsCommutative()
    {
        (from a in RationalBig from b in RationalBig select (a, b))
            .Sample(t =>
            {
                var (a, b) = t;
                Assert.Equal(a + b, b + a);
            });
    }

    [Fact]
    public void Multiplication_IsCommutative()
    {
        (from a in RationalBig from b in RationalBig select (a, b))
            .Sample(t =>
            {
                var (a, b) = t;
                Assert.Equal(a * b, b * a);
            });
    }

    [Fact]
    public void Addition_IsAssociative()
    {
        (from a in RationalSmall from b in RationalSmall from c in RationalSmall select (a, b, c))
            .Sample(t =>
            {
                var (a, b, c) = t;
                Assert.Equal((a + b) + c, a + (b + c));
            });
    }

    [Fact]
    public void Multiplication_IsAssociative()
    {
        (from a in RationalSmall from b in RationalSmall from c in RationalSmall select (a, b, c))
            .Sample(t =>
            {
                var (a, b, c) = t;
                Assert.Equal((a * b) * c, a * (b * c));
            });
    }

    [Fact]
    public void AdditiveInverse_SumsToZero()
    {
        RationalBig.Sample(a =>
        {
            Assert.Equal(Rational.Zero, a + (-a));
            Assert.Equal(Rational.Zero, a - a);
        });
    }

    [Fact]
    public void AddThenSubtract_RoundTrips()
    {
        (from a in RationalSmall from b in RationalSmall select (a, b))
            .Sample(t =>
            {
                var (a, b) = t;
                Assert.Equal(a, (a + b) - b);
            });
    }

    [Fact]
    public void MultiplyThenDivide_RoundTrips()
    {
        // b != 0 required for the division.
        (from a in RationalSmall from b in RationalSmall.Where(r => r.Numerator != 0) select (a, b))
            .Sample(t =>
            {
                var (a, b) = t;
                Assert.Equal(a, (a * b) / b);
            });
    }

    [Fact]
    public void CompareTo_SignMatchesDouble()
    {
        // Narrow bound: distinct rationals here differ by >= 1e-6, far above double rounding error,
        // so the sign of the exact comparison always matches the double comparison (ties allowed).
        (from a in RationalSmall from b in RationalSmall select (a, b))
            .Sample(t =>
            {
                var (a, b) = t;
                var cmp = Math.Sign(a.CompareTo(b));
                var dcmp = Math.Sign(a.ToDouble().CompareTo(b.ToDouble()));
                Assert.Equal(dcmp, cmp);
            });
    }

    [Fact]
    public void CompareTo_ConsistentWithEquals()
    {
        (from a in RationalBig from b in RationalBig select (a, b))
            .Sample(t =>
            {
                var (a, b) = t;
                Assert.Equal(a.Equals(b), a.CompareTo(b) == 0);
            });
    }

    [Fact]
    public void CompareTo_IsAntisymmetric()
    {
        (from a in RationalBig from b in RationalBig select (a, b))
            .Sample(t =>
            {
                var (a, b) = t;
                Assert.Equal(-Math.Sign(b.CompareTo(a)), Math.Sign(a.CompareTo(b)));
            });
    }

    [Fact]
    public void CompareTo_IsTransitive()
    {
        (from a in RationalSmall from b in RationalSmall from c in RationalSmall select (a, b, c))
            .Sample(t =>
            {
                var (a, b, c) = t;
                if (a.CompareTo(b) <= 0 && b.CompareTo(c) <= 0)
                {
                    Assert.True(a.CompareTo(c) <= 0, "<= must be transitive");
                }
            });
    }

    [Fact]
    public void Default_EqualsZeroAndComparesAsZero()
    {
        Rational def = default;
        Assert.Equal(Rational.Zero, def);
        Assert.Equal(0, def.CompareTo(Rational.Zero));

        // And every value compares against default(Rational) exactly as it does against Zero.
        RationalBig.Sample(a =>
        {
            Assert.Equal(a.CompareTo(Rational.Zero), a.CompareTo(def));
        });
    }
}

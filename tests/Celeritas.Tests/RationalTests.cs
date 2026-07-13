using Celeritas.Core;

namespace Celeritas.Tests;

public class RationalTests
{
    [Fact]
    public void Default_IsValidZero()
    {
        var def = default(Rational);

        Assert.Equal(0, def.Numerator);
        Assert.Equal(1, def.Denominator);
        Assert.Equal(Rational.Zero, def);
        Assert.Equal(0, def.CompareTo(Rational.Zero));
        Assert.True(def < Rational.Quarter);
        Assert.Equal(0.0, def.ToDouble());
    }

    [Fact]
    public void Constructor_NormalizesSignAndReduces()
    {
        Assert.Equal(new Rational(1, 2), new Rational(4, 8));
        Assert.Equal(new Rational(-1, 2), new Rational(1, -2));
        Assert.Equal(new Rational(1, 2), new Rational(-1, -2));
        Assert.Equal(new Rational(0, 1), new Rational(0, 999));
        Assert.Throws<ArgumentException>(() => new Rational(1, 0));
    }

    [Fact]
    public void Comparison_LargeValues_DoesNotOverflow()
    {
        // Historically overflowed: long.MaxValue * 2 wrapped negative and returned true
        Assert.False(new Rational(long.MaxValue, 1) < new Rational(1, 2));
        Assert.True(new Rational(long.MaxValue, 1) > new Rational(long.MaxValue - 1, 1));
        Assert.True(new Rational(long.MinValue, 1) < new Rational(1, long.MaxValue));
        Assert.Equal(1, new Rational(long.MaxValue, 1).CompareTo(new Rational(1, 2)));
    }

    [Fact]
    public void Addition_LargeDenominators_ReducesInsteadOfOverflowing()
    {
        // Naive cross-multiplication would overflow (denominator product 1.5e19 > long.MaxValue)
        var sum = new Rational(1, 3_000_000_000) + new Rational(1, 5_000_000_000);

        Assert.Equal(new Rational(8, 15_000_000_000), sum);
    }

    [Fact]
    public void Multiplication_CrossReduces()
    {
        var product = new Rational(1, 3_000_000_000) * new Rational(3_000_000_000, 7);

        Assert.Equal(new Rational(1, 7), product);
    }

    [Fact]
    public void Arithmetic_TrueOverflow_Throws()
    {
        Assert.Throws<OverflowException>(() => new Rational(long.MaxValue, 1) + new Rational(1, 1));
        Assert.Throws<OverflowException>(() => new Rational(long.MaxValue, 1) * new Rational(long.MaxValue, 1));
    }

    [Fact]
    public void Division_ByZero_Throws()
    {
        Assert.Throws<DivideByZeroException>(() => Rational.Quarter / Rational.Zero);
        Assert.Throws<DivideByZeroException>(() => Rational.Quarter / 0L);
    }

    [Fact]
    public void UnaryMinus_Negates()
    {
        Assert.Equal(new Rational(-1, 4), -Rational.Quarter);
        Assert.Equal(Rational.Quarter, -new Rational(-1, 4));
        Assert.Equal(Rational.Zero, -Rational.Zero);
    }

    [Fact]
    public void BasicArithmetic_StaysExact()
    {
        Assert.Equal(new Rational(3, 4), Rational.Quarter + Rational.Half);
        Assert.Equal(Rational.Quarter, Rational.Half - Rational.Quarter);
        Assert.Equal(Rational.Eighth, Rational.Quarter * Rational.Half);
        Assert.Equal(Rational.Half, Rational.Quarter / Rational.Half);
        Assert.Equal(Rational.Half, Rational.Quarter * 2);
        Assert.Equal(Rational.Eighth, Rational.Quarter / 2);
    }

    [Fact]
    public void CompareTo_IsConsistentWithEquality()
    {
        var a = new Rational(1, 3);
        var b = new Rational(2, 6);

        Assert.Equal(a, b);
        Assert.Equal(0, a.CompareTo(b));
        Assert.True(a <= b);
        Assert.True(a >= b);
        Assert.False(a < b);
        Assert.False(a > b);
    }
}

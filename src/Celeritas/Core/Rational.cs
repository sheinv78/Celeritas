// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core;

/// <summary>
/// High-performance rational number for precise musical time representation.
/// Automatically normalizes to the lowest terms; the denominator is always positive.
/// <para>
/// <c>default(Rational)</c> is a valid zero (0/1). Comparisons are exact (128-bit
/// cross-multiplication, no overflow). Arithmetic operators reduce intermediate values
/// via GCD and throw <see cref="OverflowException"/> if the true result does not fit
/// in a 64-bit numerator/denominator, instead of silently wrapping.
/// </para>
/// </summary>
public readonly record struct Rational : IComparable<Rational>
{
    /// <summary>The signed numerator, in lowest terms.</summary>
    public long Numerator { get; }

    /// <summary>The denominator, in lowest terms; always positive (at least 1).</summary>
    // Backed by (denominator - 1) so a zero-initialized default(Rational) reads as 1 — i.e. 0/1.
    public long Denominator => field + 1;

    /// <summary>
    /// Creates a rational number, normalized to lowest terms with a positive denominator.
    /// </summary>
    /// <param name="numerator">The numerator (any sign).</param>
    /// <param name="denominator">The denominator; must be non-zero.</param>
    /// <exception cref="ArgumentException"><paramref name="denominator"/> is zero.</exception>
    /// <exception cref="OverflowException">Normalization does not fit in a 64-bit numerator/denominator.</exception>
    public Rational(long numerator, long denominator)
    {
        if (denominator == 0)
            throw new ArgumentException("Denominator cannot be zero");

        if (numerator == 0)
        {
            Numerator = 0;
            Denominator = 0;
            return;
        }

        // Normalize: always simplify and keep denominator positive.
        // checked: -long.MinValue / MinValue-corner normalization must throw, not wrap.
        var gcd = Gcd(numerator, denominator);
        var num = numerator / gcd;
        var den = denominator / gcd;
        if (den < 0)
        {
            num = checked(-num);
            den = checked(-den);
        }
        Numerator = num;
        Denominator = den - 1;
    }

    /// <summary>Zero (0/1).</summary>
    public static Rational Zero => new(0, 1);

    /// <summary>A quarter note (1/4 of a whole note).</summary>
    public static Rational Quarter => new(1, 4);

    /// <summary>A half note (1/2 of a whole note).</summary>
    public static Rational Half => new(1, 2);

    /// <summary>A whole note (1/1), the unit of the whole-note time model.</summary>
    public static Rational Whole => new(1, 1);

    /// <summary>An eighth note (1/8 of a whole note).</summary>
    public static Rational Eighth => new(1, 8);

    /// <summary>A sixteenth note (1/16 of a whole note).</summary>
    public static Rational Sixteenth => new(1, 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Gcd(long a, long b)
    {
        // Compute on unsigned magnitudes so long.MinValue does not overflow on negation
        var x = a == long.MinValue ? (ulong)long.MaxValue + 1 : (ulong)Math.Abs(a);
        var y = b == long.MinValue ? (ulong)long.MaxValue + 1 : (ulong)Math.Abs(b);
        while (y != 0)
        {
            var temp = y;
            y = x % y;
            x = temp;
        }
        return (long)x;
    }

    /// <summary>Adds two rational numbers exactly.</summary>
    /// <exception cref="OverflowException">The exact sum does not fit in a 64-bit rational.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator +(Rational a, Rational b)
    {
        // Optimization: if denominators are equal, no need to multiply. The unchecked add plus
        // sign test keeps the fast path branch-cheap; on 64-bit overflow the exact result may
        // still be representable after reduction, so fall through to the exact path.
        if (a.Denominator == b.Denominator)
        {
            var sum = unchecked(a.Numerator + b.Numerator);
            if (((a.Numerator ^ sum) & (b.Numerator ^ sum)) >= 0)
                return new Rational(sum, a.Denominator);
        }

        return AddExact(a.Numerator, a.Denominator, b.Numerator, b.Denominator);
    }

    /// <summary>Subtracts <paramref name="b"/> from <paramref name="a"/> exactly.</summary>
    /// <exception cref="OverflowException">The exact difference does not fit in a 64-bit rational.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator -(Rational a, Rational b)
    {
        if (a.Denominator == b.Denominator)
        {
            var diff = unchecked(a.Numerator - b.Numerator);
            if (((a.Numerator ^ b.Numerator) & (a.Numerator ^ diff)) >= 0)
                return new Rational(diff, a.Denominator);
        }

        return AddExact(a.Numerator, a.Denominator, -(Int128)b.Numerator, b.Denominator);
    }

    /// <summary>
    /// Exact addition via Knuth's reduced algorithm (TAOCP 4.5.1): with g = gcd(den1, den2),
    /// the cross-term sum t (kept in 128 bits, so it cannot overflow) can only share the factor
    /// gcd(t, g) with the combined denominator. Dividing it out yields the result already in
    /// lowest terms, so an <see cref="OverflowException"/> is thrown only when the true reduced
    /// result is genuinely unrepresentable in a 64-bit rational.
    /// </summary>
    private static Rational AddExact(long aNum, long aDen, Int128 bNum, long bDen)
    {
        var g = Gcd(aDen, bDen);
        var bScale = bDen / g;
        var aScale = aDen / g;
        var t = ((Int128)aNum * bScale) + (bNum * aScale);
        if (t == 0)
            return Zero;

        // gcd(t, g) fits in long because |t % g| < g
        var g2 = Gcd((long)(t % g), g);
        var num = t / g2;
        var den = (Int128)aScale * (bDen / g2);
        return new Rational(checked((long)num), checked((long)den));
    }

    /// <summary>Returns the negation of <paramref name="a"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator -(Rational a) => new(checked(-a.Numerator), a.Denominator);

    /// <summary>Multiplies two rational numbers exactly (cross-reducing to avoid overflow).</summary>
    /// <exception cref="OverflowException">The exact product does not fit in a 64-bit rational.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator *(Rational a, Rational b)
    {
        // Cross-reduce before multiplying to avoid unnecessary overflow
        var g1 = Gcd(a.Numerator, b.Denominator);
        var g2 = Gcd(b.Numerator, a.Denominator);
        return new Rational(
            checked(a.Numerator / g1 * (b.Numerator / g2)),
            checked(a.Denominator / g2 * (b.Denominator / g1)));
    }

    /// <summary>Divides <paramref name="a"/> by <paramref name="b"/> exactly.</summary>
    /// <exception cref="DivideByZeroException"><paramref name="b"/> is zero.</exception>
    /// <exception cref="OverflowException">The exact quotient does not fit in a 64-bit rational.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator /(Rational a, Rational b)
    {
        if (b.Numerator == 0)
            throw new DivideByZeroException("Division by zero Rational");

        var g1 = Gcd(a.Numerator, b.Numerator);
        var g2 = Gcd(b.Denominator, a.Denominator);

        // Build the quotient in 128 bits and check the reduced result, not the intermediate.
        // Gcd returns a magnitude and denominators are positive, so the numerator product
        // carries the sign of `a` alone while the true sign is sign(a) XOR sign(b). Dividing by
        // a negative therefore built the numerator at +2^63 and `checked` refused it — even
        // though the constructor was about to flip it to -2^63, which is a long the type holds
        // happily: (2^62)/(-1/2) threw while the identical (-2^62)/(1/2) returned. Cross-
        // reduction above leaves the pair coprime, so what is measured here is the final answer.
        var numerator = (Int128)(a.Numerator / g1) * (b.Denominator / g2);
        var denominator = (Int128)(a.Denominator / g2) * (b.Numerator / g1);

        if (denominator < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        if (numerator < long.MinValue || numerator > long.MaxValue || denominator > long.MaxValue)
            throw new OverflowException("The exact quotient does not fit in a 64-bit rational.");

        return new Rational((long)numerator, (long)denominator);
    }

    /// <summary>Multiplies a rational number by an integer exactly.</summary>
    /// <exception cref="OverflowException">The exact product does not fit in a 64-bit rational.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator *(Rational a, long b)
    {
        var g = Gcd(a.Denominator, b);
        return new Rational(checked(a.Numerator * (b / g)), a.Denominator / g);
    }

    /// <summary>Divides a rational number by an integer exactly.</summary>
    /// <exception cref="DivideByZeroException"><paramref name="b"/> is zero.</exception>
    /// <exception cref="OverflowException">The exact quotient does not fit in a 64-bit rational.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator /(Rational a, long b)
    {
        if (b == 0)
            throw new DivideByZeroException("Division of Rational by zero");

        var g = Gcd(a.Numerator, b);
        return new Rational(a.Numerator / g, checked(a.Denominator * (b / g)));
    }

    // Comparison operators: exact via 128-bit cross-multiplication (denominators are positive)

    /// <summary>Exact less-than comparison (128-bit cross-multiplication, no overflow).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Rational a, Rational b) =>
        (Int128)a.Numerator * b.Denominator < (Int128)b.Numerator * a.Denominator;

    /// <summary>Exact greater-than comparison (128-bit cross-multiplication, no overflow).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Rational a, Rational b) =>
        (Int128)a.Numerator * b.Denominator > (Int128)b.Numerator * a.Denominator;

    /// <summary>Exact less-than-or-equal comparison (128-bit cross-multiplication, no overflow).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Rational a, Rational b) =>
        (Int128)a.Numerator * b.Denominator <= (Int128)b.Numerator * a.Denominator;

    /// <summary>Exact greater-than-or-equal comparison (128-bit cross-multiplication, no overflow).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Rational a, Rational b) =>
        (Int128)a.Numerator * b.Denominator >= (Int128)b.Numerator * a.Denominator;

    /// <summary>
    /// Compares this value with <paramref name="other"/>, returning a negative number, zero, or a
    /// positive number as this value is less than, equal to, or greater than <paramref name="other"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Rational other) =>
        ((Int128)Numerator * other.Denominator).CompareTo((Int128)other.Numerator * Denominator);

    /// <summary>Converts to the nearest <see cref="double"/> (may lose precision).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ToDouble() => (double)Numerator / Denominator;

    /// <summary>Formats as <c>"n"</c> when the denominator is 1, otherwise <c>"n/d"</c>.</summary>
    public override string ToString() => Denominator == 1 ? $"{Numerator}" : $"{Numerator}/{Denominator}";
}

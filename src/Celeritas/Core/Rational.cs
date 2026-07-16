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
    private readonly long _numerator;
    private readonly long _denominatorMinusOne; // stored minus one so default(Rational) == 0/1

    /// <summary>The signed numerator, in lowest terms.</summary>
    public long Numerator => _numerator;

    /// <summary>The denominator, in lowest terms; always positive (at least 1).</summary>
    public long Denominator => _denominatorMinusOne + 1;

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
            _numerator = 0;
            _denominatorMinusOne = 0;
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
        _numerator = num;
        _denominatorMinusOne = den - 1;
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
        // Optimization: if denominators are equal, no need to multiply
        if (a.Denominator == b.Denominator)
            return new Rational(checked(a.Numerator + b.Numerator), a.Denominator);

        // Reduce by the GCD of the denominators first to keep cross-products small
        var g = Gcd(a.Denominator, b.Denominator);
        var bScale = b.Denominator / g;
        var aScale = a.Denominator / g;
        return new Rational(
            checked((a.Numerator * bScale) + (b.Numerator * aScale)),
            checked(a.Denominator * bScale));
    }

    /// <summary>Subtracts <paramref name="b"/> from <paramref name="a"/> exactly.</summary>
    /// <exception cref="OverflowException">The exact difference does not fit in a 64-bit rational.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator -(Rational a, Rational b)
    {
        if (a.Denominator == b.Denominator)
            return new Rational(checked(a.Numerator - b.Numerator), a.Denominator);

        var g = Gcd(a.Denominator, b.Denominator);
        var bScale = b.Denominator / g;
        var aScale = a.Denominator / g;
        return new Rational(
            checked((a.Numerator * bScale) - (b.Numerator * aScale)),
            checked(a.Denominator * bScale));
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
            checked((a.Numerator / g1) * (b.Numerator / g2)),
            checked((a.Denominator / g2) * (b.Denominator / g1)));
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
        return new Rational(
            checked((a.Numerator / g1) * (b.Denominator / g2)),
            checked((a.Denominator / g2) * (b.Numerator / g1)));
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

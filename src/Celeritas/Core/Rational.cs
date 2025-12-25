// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core;

/// <summary>
/// High-performance rational number for precise musical time representation.
/// Automatically normalizes to lowest terms.
/// </summary>
public readonly record struct Rational : IComparable<Rational>
{
    public long Numerator { get; }
    public long Denominator { get; }

    public Rational(long numerator, long denominator)
    {
        if (denominator == 0)
            throw new ArgumentException("Denominator cannot be zero");

        if (numerator == 0)
        {
            Numerator = 0;
            Denominator = 1;
            return;
        }

        // Normalize: always simplify and keep denominator positive
        var gcd = Gcd(numerator, denominator);
        var sign = denominator < 0 ? -1 : 1;
        Numerator = sign * numerator / gcd;
        Denominator = sign * denominator / gcd;
    }

    public static Rational Zero => new(0, 1);
    public static Rational Quarter => new(1, 4);
    public static Rational Half => new(1, 2);
    public static Rational Whole => new(1, 1);
    public static Rational Eighth => new(1, 8);
    public static Rational Sixteenth => new(1, 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Gcd(long a, long b)
    {
        a = a < 0 ? -a : a;
        b = b < 0 ? -b : b;
        while (b != 0)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }

    // Rational automatically normalizes on construction, so operators don't need to

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator +(Rational a, Rational b)
    {
        // Optimization: if denominators are equal, no need to multiply
        if (a.Denominator == b.Denominator)
            return new Rational(a.Numerator + b.Numerator, a.Denominator);

        return new Rational(
            a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator -(Rational a, Rational b)
    {
        if (a.Denominator == b.Denominator)
            return new Rational(a.Numerator - b.Numerator, a.Denominator);

        return new Rational(
            a.Numerator * b.Denominator - b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator *(Rational a, Rational b) =>
        new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator /(Rational a, Rational b) =>
        new(a.Numerator * b.Denominator, a.Denominator * b.Numerator);

    // Fast multiply/divide by an integer
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator *(Rational a, long b) => new(a.Numerator * b, a.Denominator);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rational operator /(Rational a, long b) => new(a.Numerator, a.Denominator * b);

    // Comparison operators without creating temporaries
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Rational a, Rational b) =>
        a.Numerator * b.Denominator < b.Numerator * a.Denominator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Rational a, Rational b) =>
        a.Numerator * b.Denominator > b.Numerator * a.Denominator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Rational a, Rational b) =>
        a.Numerator * b.Denominator <= b.Numerator * a.Denominator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Rational a, Rational b) =>
        a.Numerator * b.Denominator >= b.Numerator * a.Denominator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Rational other)
    {
        var diff = Numerator * other.Denominator - other.Numerator * Denominator;
        return diff < 0 ? -1 : diff > 0 ? 1 : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ToDouble() => (double)Numerator / Denominator;

    public override string ToString() => Denominator == 1 ? $"{Numerator}" : $"{Numerator}/{Denominator}";
}

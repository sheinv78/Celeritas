// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core;

/// <summary>
/// Time signature / meter.
/// </summary>
/// <remarks>
/// Both parts must be positive. <c>BeatUnit</c> is a denominator: at zero, <c>BeatDuration</c> and
/// <c>MeasureDuration</c> are undefined, and the meter still prints and compares as if it were a
/// real one ("4/0"). Note this only covers explicit construction — <c>default(TimeSignature)</c>
/// bypasses these initializers and is 0/0, as it is for every C# struct.
/// Whether a denominator is writable to a MIDI file is a stricter question, and belongs to the
/// export path rather than here: MIDI stores log2 of the denominator, so it can only encode powers
/// of two, while a meter like 4/3 is representable in this type and meaningful on paper.
/// </remarks>
/// <exception cref="ArgumentOutOfRangeException">
/// <paramref name="beatsPerMeasure"/> or <paramref name="beatUnit"/> is not positive.
/// </exception>
public readonly struct TimeSignature(int beatsPerMeasure, int beatUnit) : IEquatable<TimeSignature>
{
    /// <summary>Beats per measure (numerator).</summary>
    public int BeatsPerMeasure { get; } = ThrowIfNotPositive(beatsPerMeasure);

    /// <summary>Beat unit as note value (4 = quarter, 8 = eighth, etc).</summary>
    public int BeatUnit { get; } = ThrowIfNotPositive(beatUnit);

    private static int ThrowIfNotPositive(int value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, name);
        return value;
    }

    /// <summary>Duration of one beat as a Rational.</summary>
    public Rational BeatDuration => new(1, BeatUnit);

    /// <summary>Duration of one measure.</summary>
    public Rational MeasureDuration => new(BeatsPerMeasure, BeatUnit);

    /// <summary>Common time signatures.</summary>
    public static TimeSignature Common => new(4, 4);
    public static TimeSignature CutTime => new(2, 2);
    public static TimeSignature Waltz => new(3, 4);
    public static TimeSignature Compound6 => new(6, 8);
    public static TimeSignature Compound9 => new(9, 8);
    public static TimeSignature Compound12 => new(12, 8);

    /// <summary>Is this a compound meter (beats subdivide into 3)?</summary>
    public bool IsCompound => BeatsPerMeasure is 6 or 9 or 12 && BeatUnit is 4 or 8;

    /// <summary>Is this a simple meter (beats subdivide into 2)?</summary>
    public bool IsSimple => !IsCompound;

    /// <summary>Number of strong beats per measure.</summary>
    public int StrongBeats => IsCompound ? BeatsPerMeasure / 3 : BeatsPerMeasure switch
    {
        2 => 1,
        3 => 1,
        4 => 2,
        _ => 1
    };

    public override string ToString() => $"{BeatsPerMeasure}/{BeatUnit}";

    public bool Equals(TimeSignature other) => BeatsPerMeasure == other.BeatsPerMeasure && BeatUnit == other.BeatUnit;
    public override bool Equals(object? obj) => obj is TimeSignature other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(BeatsPerMeasure, BeatUnit);
    public static bool operator ==(TimeSignature left, TimeSignature right) => left.Equals(right);
    public static bool operator !=(TimeSignature left, TimeSignature right) => !left.Equals(right);
}

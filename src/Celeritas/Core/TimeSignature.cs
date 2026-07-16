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
/// <param name="beatsPerMeasure">Beats per measure (numerator); must be positive.</param>
/// <param name="beatUnit">Beat unit note value (denominator: 4 = quarter, 8 = eighth); must be positive.</param>
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

    /// <summary>Common time, 4/4.</summary>
    public static TimeSignature Common => new(4, 4);
    /// <summary>Cut time, 2/2.</summary>
    public static TimeSignature CutTime => new(2, 2);
    /// <summary>Waltz meter, 3/4.</summary>
    public static TimeSignature Waltz => new(3, 4);
    /// <summary>Compound duple, 6/8.</summary>
    public static TimeSignature Compound6 => new(6, 8);
    /// <summary>Compound triple, 9/8.</summary>
    public static TimeSignature Compound9 => new(9, 8);
    /// <summary>Compound quadruple, 12/8.</summary>
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

    /// <summary>Returns the meter as "beats/unit" (e.g. "4/4").</summary>
    public override string ToString() => $"{BeatsPerMeasure}/{BeatUnit}";

    /// <summary>Indicates whether this meter equals <paramref name="other"/> (same numerator and denominator).</summary>
    public bool Equals(TimeSignature other) => BeatsPerMeasure == other.BeatsPerMeasure && BeatUnit == other.BeatUnit;
    /// <summary>Indicates whether <paramref name="obj"/> is an equal <see cref="TimeSignature"/>.</summary>
    public override bool Equals(object? obj) => obj is TimeSignature other && Equals(other);
    /// <summary>Returns a hash code combining numerator and denominator.</summary>
    public override int GetHashCode() => HashCode.Combine(BeatsPerMeasure, BeatUnit);
    /// <summary>Indicates whether two time signatures are equal.</summary>
    public static bool operator ==(TimeSignature left, TimeSignature right) => left.Equals(right);
    /// <summary>Indicates whether two time signatures differ.</summary>
    public static bool operator !=(TimeSignature left, TimeSignature right) => !left.Equals(right);
}

// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Time signature / meter.
/// </summary>
public readonly struct TimeSignature(int beatsPerMeasure, int beatUnit) : IEquatable<TimeSignature>
{
    /// <summary>Beats per measure (numerator).</summary>
    public int BeatsPerMeasure { get; } = beatsPerMeasure;

    /// <summary>Beat unit as note value (4 = quarter, 8 = eighth, etc).</summary>
    public int BeatUnit { get; } = beatUnit;

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

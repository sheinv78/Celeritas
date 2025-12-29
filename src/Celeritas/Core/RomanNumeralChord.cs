// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Result of roman numeral analysis
/// </summary>
public readonly struct RomanNumeralChord
{
    public readonly ScaleDegree Degree;
    public readonly ChordQuality Quality;
    public readonly HarmonicFunction Function;
    public readonly bool IsValid;

    private static readonly byte[] MajorTriadIntervals = [0, 4, 7];
    private static readonly byte[] MinorTriadIntervals = [0, 3, 7];
    private static readonly byte[] DiminishedTriadIntervals = [0, 3, 6];
    private static readonly byte[] AugmentedTriadIntervals = [0, 4, 8];
    private static readonly byte[] Sus2Intervals = [0, 2, 7];
    private static readonly byte[] Sus4Intervals = [0, 5, 7];
    private static readonly byte[] PowerIntervals = [0, 7];
    private static readonly byte[] QuartalIntervals = [0, 5, 10];

    private static readonly byte[] Major7Intervals = [0, 4, 7, 11];
    private static readonly byte[] Minor7Intervals = [0, 3, 7, 10];
    private static readonly byte[] Dominant7Intervals = [0, 4, 7, 10];
    private static readonly byte[] Dominant7Flat5Intervals = [0, 4, 6, 10];
    private static readonly byte[] Diminished7Intervals = [0, 3, 6, 9];
    private static readonly byte[] HalfDim7Intervals = [0, 3, 6, 10];
    private static readonly byte[] MinorMajor7Intervals = [0, 3, 7, 11];
    private static readonly byte[] Augmented7Intervals = [0, 4, 8, 10];

    private static readonly byte[] Add9Intervals = [0, 4, 7, 2];
    private static readonly byte[] Add11Intervals = [0, 4, 7, 5];

    public RomanNumeralChord(ScaleDegree degree, ChordQuality quality, HarmonicFunction function)
    {
        Degree = degree;
        Quality = quality;
        Function = function;
        IsValid = true;
    }

    public static RomanNumeralChord Invalid => new();

    /// <summary>
    /// Gets the pitch class (0-11) of this chord's root in the given key.
    /// </summary>
    public byte GetRootPitchClass(KeySignature key)
    {
        return key.GetScaleDegreePitchClass(Degree);
    }

    /// <summary>
    /// Writes chord pitch classes (0-11) into destination and returns the number written.
    /// The chord is spelled from its root using the current <see cref="Quality"/>.
    /// </summary>
    public int WritePitchClasses(KeySignature key, Span<byte> destination)
    {
        if (!IsValid)
            return 0;

        var intervals = GetQualityIntervals(Quality);
        if (intervals.IsEmpty)
            return 0;

        if (destination.Length < intervals.Length)
            throw new ArgumentException("Destination span too small", nameof(destination));

        var root = GetRootPitchClass(key);
        for (var i = 0; i < intervals.Length; i++)
        {
            destination[i] = (byte)((root + intervals[i]) % 12);
        }

        return intervals.Length;
    }

    /// <summary>
    /// Returns chord pitch classes (0-11) as an array.
    /// </summary>
    public byte[] GetPitchClasses(KeySignature key)
    {
        if (!IsValid)
            return [];

        var intervals = GetQualityIntervals(Quality);
        if (intervals.IsEmpty)
            return [];

        var pitchClasses = new byte[intervals.Length];
        WritePitchClasses(key, pitchClasses);
        return pitchClasses;
    }

    /// <summary>
    /// Returns a 12-bit pitch-class mask for this chord in the given key.
    /// </summary>
    public ushort GetPitchClassMask(KeySignature key)
    {
        if (!IsValid)
            return 0;

        var intervals = GetQualityIntervals(Quality);
        if (intervals.IsEmpty)
            return 0;

        var root = GetRootPitchClass(key);
        ushort mask = 0;
        for (var i = 0; i < intervals.Length; i++)
        {
            mask |= (ushort)(1 << ((root + intervals[i]) % 12));
        }

        return mask;
    }

    private static ReadOnlySpan<byte> GetQualityIntervals(ChordQuality quality)
    {
        return quality switch
        {
            ChordQuality.Major => MajorTriadIntervals,
            ChordQuality.Minor => MinorTriadIntervals,
            ChordQuality.Diminished => DiminishedTriadIntervals,
            ChordQuality.Augmented => AugmentedTriadIntervals,
            ChordQuality.Sus2 => Sus2Intervals,
            ChordQuality.Sus4 => Sus4Intervals,
            ChordQuality.Power => PowerIntervals,
            ChordQuality.Quartal => QuartalIntervals,

            ChordQuality.Major7 => Major7Intervals,
            ChordQuality.Minor7 => Minor7Intervals,
            ChordQuality.Dominant7 => Dominant7Intervals,
            ChordQuality.Dominant7Flat5 => Dominant7Flat5Intervals,
            ChordQuality.Diminished7 => Diminished7Intervals,
            ChordQuality.HalfDim7 => HalfDim7Intervals,
            ChordQuality.MinorMajor7 => MinorMajor7Intervals,
            ChordQuality.Augmented7 => Augmented7Intervals,

            ChordQuality.Add9 => Add9Intervals,
            ChordQuality.Add11 => Add11Intervals,

            _ => ReadOnlySpan<byte>.Empty
        };
    }

    public string ToRomanNumeral()
    {
        if (!IsValid) return "?";

        var numeral = Degree switch
        {
            ScaleDegree.I => "I",
            ScaleDegree.II => "II",
            ScaleDegree.III => "III",
            ScaleDegree.IV => "IV",
            ScaleDegree.V => "V",
            ScaleDegree.VI => "VI",
            ScaleDegree.VII => "VII",
            _ => "?"
        };

        // Lowercase for minor/diminished qualities in traditional notation
        if (Quality is ChordQuality.Minor or ChordQuality.Diminished or ChordQuality.Minor7 or ChordQuality.HalfDim7 or ChordQuality.Diminished7 or ChordQuality.MinorMajor7)
        {
            numeral = numeral.ToLowerInvariant();
        }

        // Add quality suffix
        var suffix = Quality switch
        {
            ChordQuality.Dominant7 => "7",
            ChordQuality.Dominant7Flat5 => "7b5",
            ChordQuality.Major7 => "maj7",
            ChordQuality.Minor7 => "7",
            ChordQuality.HalfDim7 => "ø7",
            ChordQuality.Diminished => "°",
            ChordQuality.Diminished7 => "°7",
            ChordQuality.MinorMajor7 => "maj7",
            _ => ""
        };

        return numeral + suffix;
    }

    public override string ToString() => $"{ToRomanNumeral()} ({Function})";
}

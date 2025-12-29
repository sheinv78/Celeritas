// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

public enum DiatonicChordType
{
    Triad,
    Seventh
}

public enum MinorDominantStyle
{
    /// <summary>
    /// Natural minor (v / v7).
    /// </summary>
    Natural,

    /// <summary>
    /// Harmonic-minor functional dominant (V / V7).
    /// Only affects the dominant chord quality (degree V).
    /// </summary>
    Harmonic
}

/// <summary>
/// A diatonic functional chord described by roman numeral + key.
/// </summary>
public readonly record struct FunctionalChord(KeySignature Key, RomanNumeralChord Roman)
{
    public byte RootPitchClass => Roman.GetRootPitchClass(Key);

    public PitchClass Root => new(RootPitchClass);

    public string RomanNumeral => Roman.ToRomanNumeral();

    public string RootName(bool preferSharps = true) => Root.ToName(preferSharps);

    public string Symbol(bool preferSharps = true)
    {
        var root = RootName(preferSharps);

        return Roman.Quality switch
        {
            ChordQuality.Major => root,
            ChordQuality.Minor => root + "m",
            ChordQuality.Diminished => root + "dim",

            ChordQuality.Major7 => root + "maj7",
            ChordQuality.Minor7 => root + "m7",
            ChordQuality.Dominant7 => root + "7",
            ChordQuality.Dominant7Flat5 => root + "7b5",
            ChordQuality.HalfDim7 => root + "m7b5",

            _ => root + " " + Roman.Quality
        };
    }

    public ushort PitchClassMask => Roman.GetPitchClassMask(Key);
}

/// <summary>
/// Functional-harmony progressions (diatonic by default).
/// Includes circle-of-fifths (descending fifths) chains and common cadential patterns.
/// </summary>
public static class FunctionalProgressions
{
    private static readonly ScaleDegree[] CircleDegrees =
    [
        ScaleDegree.I,
        ScaleDegree.IV,
        ScaleDegree.VII,
        ScaleDegree.III,
        ScaleDegree.VI,
        ScaleDegree.II,
        ScaleDegree.V,
        ScaleDegree.I
    ];

    public static FunctionalChord[] Circle(KeySignature key, DiatonicChordType type = DiatonicChordType.Seventh, MinorDominantStyle minorDominant = MinorDominantStyle.Harmonic)
    {
        var result = new FunctionalChord[CircleDegrees.Length];
        for (var i = 0; i < CircleDegrees.Length; i++)
        {
            var roman = MakeDiatonic(key, CircleDegrees[i], type, minorDominant);
            result[i] = new FunctionalChord(key, roman);
        }
        return result;
    }

    public static FunctionalChord[] TwoFiveOne(KeySignature key, DiatonicChordType type = DiatonicChordType.Seventh, MinorDominantStyle minorDominant = MinorDominantStyle.Harmonic)
    {
        var degrees = new[] { ScaleDegree.II, ScaleDegree.V, ScaleDegree.I };
        var result = new FunctionalChord[degrees.Length];
        for (var i = 0; i < degrees.Length; i++)
        {
            var roman = MakeDiatonic(key, degrees[i], type, minorDominant);
            result[i] = new FunctionalChord(key, roman);
        }
        return result;
    }

    /// <summary>
    /// Common turnaround: I → vi → ii → V → I.
    /// In minor: i → VI → ii° → V → i.
    /// </summary>
    public static FunctionalChord[] Turnaround(KeySignature key, DiatonicChordType type = DiatonicChordType.Seventh, MinorDominantStyle minorDominant = MinorDominantStyle.Harmonic)
    {
        var degrees = new[] { ScaleDegree.I, ScaleDegree.VI, ScaleDegree.II, ScaleDegree.V, ScaleDegree.I };
        var result = new FunctionalChord[degrees.Length];
        for (var i = 0; i < degrees.Length; i++)
        {
            var roman = MakeDiatonic(key, degrees[i], type, minorDominant);
            result[i] = new FunctionalChord(key, roman);
        }
        return result;
    }

    /// <summary>
    /// Common extended cadence: iii → vi → ii → V → I.
    /// In minor: III → VI → ii°/iiø → V → i (depending on <paramref name="minorDominant"/>).
    /// </summary>
    public static FunctionalChord[] ThreeSixTwoFiveOne(KeySignature key, DiatonicChordType type = DiatonicChordType.Seventh, MinorDominantStyle minorDominant = MinorDominantStyle.Harmonic)
    {
        var degrees = new[] { ScaleDegree.III, ScaleDegree.VI, ScaleDegree.II, ScaleDegree.V, ScaleDegree.I };
        var result = new FunctionalChord[degrees.Length];
        for (var i = 0; i < degrees.Length; i++)
        {
            var roman = MakeDiatonic(key, degrees[i], type, minorDominant);
            result[i] = new FunctionalChord(key, roman);
        }
        return result;
    }

    /// <summary>
    /// Secondary dominant leading to a diatonic scale degree.
    /// Example in C major: SecondaryDominantTo(ii) => A7 (V7/ii)
    /// </summary>
    public static SecondaryDominant SecondaryDominantTo(KeySignature key, ScaleDegree targetDegree, DiatonicChordType type = DiatonicChordType.Seventh)
    {
        return new SecondaryDominant(key, targetDegree, type);
    }

    /// <summary>
    /// Common set of secondary dominants: V/(ii, iii, IV, V, vi).
    /// </summary>
    public static SecondaryDominant[] SecondaryDominants(KeySignature key, DiatonicChordType type = DiatonicChordType.Seventh)
    {
        var targets = new[] { ScaleDegree.II, ScaleDegree.III, ScaleDegree.IV, ScaleDegree.V, ScaleDegree.VI };
        var result = new SecondaryDominant[targets.Length];
        for (var i = 0; i < targets.Length; i++)
        {
            result[i] = new SecondaryDominant(key, targets[i], type);
        }
        return result;
    }

    private static RomanNumeralChord MakeDiatonic(KeySignature key, ScaleDegree degree, DiatonicChordType type, MinorDominantStyle minorDominant)
    {
        var quality = key.IsMajor
            ? MajorQuality(degree, type)
            : MinorQuality(degree, type, minorDominant);

        var function = degree switch
        {
            ScaleDegree.I or ScaleDegree.III or ScaleDegree.VI => HarmonicFunction.Tonic,
            ScaleDegree.II or ScaleDegree.IV => HarmonicFunction.Subdominant,
            ScaleDegree.V or ScaleDegree.VII => HarmonicFunction.Dominant,
            _ => HarmonicFunction.Tonic
        };

        return new RomanNumeralChord(degree, quality, function);
    }

    private static ChordQuality MajorQuality(ScaleDegree degree, DiatonicChordType type)
    {
        return type switch
        {
            DiatonicChordType.Triad => degree switch
            {
                ScaleDegree.I or ScaleDegree.IV or ScaleDegree.V => ChordQuality.Major,
                ScaleDegree.II or ScaleDegree.III or ScaleDegree.VI => ChordQuality.Minor,
                ScaleDegree.VII => ChordQuality.Diminished,
                _ => ChordQuality.Major
            },
            _ => degree switch
            {
                ScaleDegree.I or ScaleDegree.IV => ChordQuality.Major7,
                ScaleDegree.II or ScaleDegree.III or ScaleDegree.VI => ChordQuality.Minor7,
                ScaleDegree.V => ChordQuality.Dominant7,
                ScaleDegree.VII => ChordQuality.HalfDim7,
                _ => ChordQuality.Major7
            }
        };
    }

    private static ChordQuality MinorQuality(ScaleDegree degree, DiatonicChordType type, MinorDominantStyle minorDominant)
    {
        var dominantTriad = minorDominant == MinorDominantStyle.Harmonic ? ChordQuality.Major : ChordQuality.Minor;
        var dominantSeventh = minorDominant == MinorDominantStyle.Harmonic ? ChordQuality.Dominant7 : ChordQuality.Minor7;

        return type switch
        {
            DiatonicChordType.Triad => degree switch
            {
                ScaleDegree.I or ScaleDegree.IV => ChordQuality.Minor,
                ScaleDegree.V => dominantTriad,
                ScaleDegree.II => ChordQuality.Diminished,
                ScaleDegree.III or ScaleDegree.VI or ScaleDegree.VII => ChordQuality.Major,
                _ => ChordQuality.Minor
            },
            _ => degree switch
            {
                ScaleDegree.I or ScaleDegree.IV => ChordQuality.Minor7,
                ScaleDegree.V => dominantSeventh,
                ScaleDegree.II => ChordQuality.HalfDim7,
                ScaleDegree.III or ScaleDegree.VI => ChordQuality.Major7,
                ScaleDegree.VII => ChordQuality.Dominant7,
                _ => ChordQuality.Minor7
            }
        };
    }
}

/// <summary>
/// Secondary dominant chord (V or V7) targeting a diatonic degree.
/// This is intentionally modeled as chromatic (non-diatonic) harmony.
/// </summary>
public readonly record struct SecondaryDominant(KeySignature Key, ScaleDegree TargetDegree, DiatonicChordType Type)
{
    public PitchClass TargetRoot => new(Key.GetScaleDegreePitchClass(TargetDegree));

    public PitchClass Root => CircleOfFifths.NextFifth(TargetRoot); // dominant is a fifth above target

    public string RomanNumeral => Type == DiatonicChordType.Seventh
        ? $"V7/{TargetDegree.ToString().ToLowerInvariant()}"
        : $"V/{TargetDegree.ToString().ToLowerInvariant()}";

    public string Symbol(bool preferSharps = true)
    {
        var root = Root.ToName(preferSharps);
        return Type == DiatonicChordType.Seventh ? root + "7" : root;
    }
}

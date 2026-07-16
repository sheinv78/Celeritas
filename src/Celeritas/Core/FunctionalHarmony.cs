// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Whether a diatonic chord is built as a triad or a seventh chord.
/// </summary>
public enum DiatonicChordType
{
    /// <summary>Three-note triad.</summary>
    Triad,

    /// <summary>Four-note seventh chord.</summary>
    Seventh
}

/// <summary>
/// How the dominant (degree V) is treated in a minor key.
/// </summary>
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
/// <param name="Key">The key the chord is analyzed in.</param>
/// <param name="Roman">The roman-numeral chord within that key.</param>
public readonly record struct FunctionalChord(KeySignature Key, RomanNumeralChord Roman)
{
    /// <summary>Root pitch class (0-11) of the chord in <see cref="Key"/>.</summary>
    public byte RootPitchClass => Roman.GetRootPitchClass(Key);

    /// <summary>Root of the chord as a pitch class.</summary>
    public PitchClass Root => new(RootPitchClass);

    /// <summary>Roman-numeral text for this chord (e.g. "ii7", "V").</summary>
    public string RomanNumeral => Roman.ToRomanNumeral();

    /// <summary>Root note name (e.g. "C", "F#").</summary>
    /// <param name="preferSharps">Whether to spell accidentals as sharps rather than flats.</param>
    public string RootName(bool preferSharps = true) => Root.ToName(preferSharps);

    /// <summary>Chord symbol for this chord (e.g. "Dm7", "Gmaj7").</summary>
    /// <param name="preferSharps">Whether to spell accidentals as sharps rather than flats.</param>
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

    /// <summary>12-bit pitch-class mask of this chord in <see cref="Key"/>.</summary>
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
        ScaleDegree.Iv,
        ScaleDegree.Vii,
        ScaleDegree.Iii,
        ScaleDegree.Vi,
        ScaleDegree.Ii,
        ScaleDegree.V,
        ScaleDegree.I
    ];

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined <see cref="DiatonicChordType"/> value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minorDominant"/> is not a defined <see cref="MinorDominantStyle"/> value.</exception>
    public static FunctionalChord[] Circle(KeySignature key, DiatonicChordType type = DiatonicChordType.Seventh, MinorDominantStyle minorDominant = MinorDominantStyle.Harmonic)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a defined DiatonicChordType value.");
        if (!Enum.IsDefined(minorDominant))
            throw new ArgumentOutOfRangeException(nameof(minorDominant), minorDominant, "Not a defined MinorDominantStyle value.");

        var result = new FunctionalChord[CircleDegrees.Length];
        for (var i = 0; i < CircleDegrees.Length; i++)
        {
            var roman = MakeDiatonic(key, CircleDegrees[i], type, minorDominant);
            result[i] = new FunctionalChord(key, roman);
        }
        return result;
    }

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined <see cref="DiatonicChordType"/> value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minorDominant"/> is not a defined <see cref="MinorDominantStyle"/> value.</exception>
    public static FunctionalChord[] TwoFiveOne(KeySignature key, DiatonicChordType type = DiatonicChordType.Seventh, MinorDominantStyle minorDominant = MinorDominantStyle.Harmonic)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a defined DiatonicChordType value.");
        if (!Enum.IsDefined(minorDominant))
            throw new ArgumentOutOfRangeException(nameof(minorDominant), minorDominant, "Not a defined MinorDominantStyle value.");

        var degrees = new[] { ScaleDegree.Ii, ScaleDegree.V, ScaleDegree.I };
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined <see cref="DiatonicChordType"/> value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minorDominant"/> is not a defined <see cref="MinorDominantStyle"/> value.</exception>
    public static FunctionalChord[] Turnaround(KeySignature key, DiatonicChordType type = DiatonicChordType.Seventh, MinorDominantStyle minorDominant = MinorDominantStyle.Harmonic)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a defined DiatonicChordType value.");
        if (!Enum.IsDefined(minorDominant))
            throw new ArgumentOutOfRangeException(nameof(minorDominant), minorDominant, "Not a defined MinorDominantStyle value.");

        var degrees = new[] { ScaleDegree.I, ScaleDegree.Vi, ScaleDegree.Ii, ScaleDegree.V, ScaleDegree.I };
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined <see cref="DiatonicChordType"/> value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minorDominant"/> is not a defined <see cref="MinorDominantStyle"/> value.</exception>
    public static FunctionalChord[] ThreeSixTwoFiveOne(KeySignature key, DiatonicChordType type = DiatonicChordType.Seventh, MinorDominantStyle minorDominant = MinorDominantStyle.Harmonic)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a defined DiatonicChordType value.");
        if (!Enum.IsDefined(minorDominant))
            throw new ArgumentOutOfRangeException(nameof(minorDominant), minorDominant, "Not a defined MinorDominantStyle value.");

        var degrees = new[] { ScaleDegree.Iii, ScaleDegree.Vi, ScaleDegree.Ii, ScaleDegree.V, ScaleDegree.I };
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="targetDegree"/> is not a defined <see cref="ScaleDegree"/> value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined <see cref="DiatonicChordType"/> value.</exception>
    public static SecondaryDominant SecondaryDominantTo(KeySignature key, ScaleDegree targetDegree, DiatonicChordType type = DiatonicChordType.Seventh)
    {
        if (!Enum.IsDefined(targetDegree))
            throw new ArgumentOutOfRangeException(nameof(targetDegree), targetDegree, "Not a defined ScaleDegree value.");
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a defined DiatonicChordType value.");

        return new SecondaryDominant(key, targetDegree, type);
    }

    /// <summary>
    /// Common set of secondary dominants: V/(ii, iii, IV, V, vi).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined <see cref="DiatonicChordType"/> value.</exception>
    public static SecondaryDominant[] SecondaryDominants(KeySignature key, DiatonicChordType type = DiatonicChordType.Seventh)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a defined DiatonicChordType value.");

        var targets = new[] { ScaleDegree.Ii, ScaleDegree.Iii, ScaleDegree.Iv, ScaleDegree.V, ScaleDegree.Vi };
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
            ScaleDegree.I or ScaleDegree.Iii or ScaleDegree.Vi => HarmonicFunction.Tonic,
            ScaleDegree.Ii or ScaleDegree.Iv => HarmonicFunction.Subdominant,
            ScaleDegree.V or ScaleDegree.Vii => HarmonicFunction.Dominant,
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
                ScaleDegree.I or ScaleDegree.Iv or ScaleDegree.V => ChordQuality.Major,
                ScaleDegree.Ii or ScaleDegree.Iii or ScaleDegree.Vi => ChordQuality.Minor,
                ScaleDegree.Vii => ChordQuality.Diminished,
                _ => ChordQuality.Major
            },
            _ => degree switch
            {
                ScaleDegree.I or ScaleDegree.Iv => ChordQuality.Major7,
                ScaleDegree.Ii or ScaleDegree.Iii or ScaleDegree.Vi => ChordQuality.Minor7,
                ScaleDegree.V => ChordQuality.Dominant7,
                ScaleDegree.Vii => ChordQuality.HalfDim7,
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
                ScaleDegree.I or ScaleDegree.Iv => ChordQuality.Minor,
                ScaleDegree.V => dominantTriad,
                ScaleDegree.Ii => ChordQuality.Diminished,
                ScaleDegree.Iii or ScaleDegree.Vi or ScaleDegree.Vii => ChordQuality.Major,
                _ => ChordQuality.Minor
            },
            _ => degree switch
            {
                ScaleDegree.I or ScaleDegree.Iv => ChordQuality.Minor7,
                ScaleDegree.V => dominantSeventh,
                ScaleDegree.Ii => ChordQuality.HalfDim7,
                ScaleDegree.Iii or ScaleDegree.Vi => ChordQuality.Major7,
                ScaleDegree.Vii => ChordQuality.Dominant7,
                _ => ChordQuality.Minor7
            }
        };
    }
}

/// <summary>
/// Secondary dominant chord (V or V7) targeting a diatonic degree.
/// This is intentionally modeled as chromatic (non-diatonic) harmony.
/// </summary>
/// <param name="Key">The home key.</param>
/// <param name="TargetDegree">The diatonic degree the dominant resolves to.</param>
/// <param name="Type">Whether the dominant is a triad or a seventh chord.</param>
public readonly record struct SecondaryDominant(KeySignature Key, ScaleDegree TargetDegree, DiatonicChordType Type)
{
    /// <summary>Pitch class of the target degree's root in <see cref="Key"/>.</summary>
    public PitchClass TargetRoot => new(Key.GetScaleDegreePitchClass(TargetDegree));

    /// <summary>Root of the secondary dominant, a fifth above <see cref="TargetRoot"/>.</summary>
    public PitchClass Root => CircleOfFifths.NextFifth(TargetRoot); // dominant is a fifth above target

    /// <summary>Roman-numeral text (e.g. "V7/ii").</summary>
    public string RomanNumeral => Type == DiatonicChordType.Seventh
        ? $"V7/{TargetDegree.ToString().ToLowerInvariant()}"
        : $"V/{TargetDegree.ToString().ToLowerInvariant()}";

    /// <summary>Chord symbol for this secondary dominant (e.g. "A7").</summary>
    /// <param name="preferSharps">Whether to spell accidentals as sharps rather than flats.</param>
    public string Symbol(bool preferSharps = true)
    {
        var root = Root.ToName(preferSharps);
        return Type == DiatonicChordType.Seventh ? root + "7" : root;
    }
}

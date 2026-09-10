// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core;

/// <summary>
/// Recognized chord qualities.
/// </summary>
public enum ChordQuality : byte
{
    /// <summary>
    /// Unrecognized or unclassified chord.
    /// </summary>
    /// <remarks>
    /// When the library answers with this it pairs it with root pitch class 0, because an
    /// unrecognized set has no root to report — so a <see cref="ChordInfo"/> for E-G-B-D-F#
    /// reads "C Unknown" and the C is a placeholder, not a detected root. Test
    /// <see cref="ChordInfo.Quality"/> against this value; do not read the root beside it.
    /// </remarks>
    Unknown,

    /// <summary>Major triad (root, major third, perfect fifth).</summary>
    Major,

    /// <summary>Minor triad (root, minor third, perfect fifth).</summary>
    Minor,

    /// <summary>Diminished triad (root, minor third, diminished fifth).</summary>
    Diminished,

    /// <summary>Augmented triad (root, major third, augmented fifth).</summary>
    Augmented,

    /// <summary>Major seventh chord (major triad plus a major seventh).</summary>
    Major7,

    /// <summary>Minor seventh chord (minor triad plus a minor seventh).</summary>
    Minor7,

    /// <summary>Dominant seventh chord (major triad plus a minor seventh).</summary>
    Dominant7,

    /// <summary>Diminished seventh chord (diminished triad plus a diminished seventh).</summary>
    Diminished7,

    /// <summary>Half-diminished seventh chord (diminished triad plus a minor seventh).</summary>
    HalfDim7,

    /// <summary>Suspended second (root, major second, perfect fifth).</summary>
    Sus2,

    /// <summary>Suspended fourth (root, perfect fourth, perfect fifth).</summary>
    Sus4,

    /// <summary>Power chord: root and perfect fifth dyad, no third.</summary>
    Power,          // 5th chord (no 3rd)

    /// <summary>Quartal chord built on stacked perfect fourths.</summary>
    Quartal,        // Built on 4ths

    /// <summary>Major triad with an added ninth (the second).</summary>
    Add9,

    /// <summary>Major triad with an added eleventh (the fourth).</summary>
    Add11,

    /// <summary>Minor-major seventh chord (minor triad plus a major seventh).</summary>
    MinorMajor7,

    /// <summary>Augmented seventh chord (augmented triad plus a minor seventh).</summary>
    Augmented7,

    /// <summary>Dominant seventh chord with a flatted fifth.</summary>
    Dominant7Flat5,

    /// <summary>Major sixth chord (major triad plus a major sixth).</summary>
    /// <remarks>
    /// C-E-G-A. The same four pitch classes as the minor seventh a minor third below (Am7), so
    /// only the bass tells them apart — see <see cref="ChordAnalyzer.Identify(ReadOnlySpan{int})"/>.
    /// A bright, fully stable sonority and one of the commonest ways to voice a final tonic.
    /// </remarks>
    Major6,

    /// <summary>Minor sixth chord (minor triad plus a major sixth).</summary>
    /// <remarks>
    /// C-Eb-G-A. The same four pitch classes as the half-diminished seventh a minor third below
    /// (Am7b5), so only the bass tells them apart. The tonic of the melodic minor scale, and a
    /// resting chord — not the unresolved sonority its half-diminished rotation is.
    /// </remarks>
    Minor6
}

/// <summary>
/// Compact chord info (8 bytes instead of 24+ for class)
/// </summary>
/// <param name="RootPitchClass">Root pitch class of the chord (0=C .. 11=B).</param>
/// <param name="Quality">The chord's quality.</param>
public readonly record struct ChordInfo(byte RootPitchClass, ChordQuality Quality)
{
    /// <summary>Root note name (e.g. "C", "F#") for <see cref="RootPitchClass"/>.</summary>
    /// <remarks>
    /// Meaningless when <see cref="Quality"/> is <see cref="ChordQuality.Unknown"/>: the library
    /// pairs that with pitch class 0, so this reads "C" for a chord it did not recognize at all.
    /// </remarks>
    public string Root => ChordLibrary.NoteNames[RootPitchClass];

    /// <summary>Returns the root name followed by the quality (e.g. "C Major").</summary>
    /// <remarks>
    /// The two fields are rendered as given, so an unrecognized chord reads "C Unknown" whatever
    /// notes it was — see <see cref="ChordQuality.Unknown"/>.
    /// </remarks>
    public override string ToString() => $"{Root} {Quality}";
}

/// <summary>
/// Lookup table mapping 12-bit pitch-class masks to recognized chords.
/// </summary>
public static class ChordLibrary
{
    // Lookup array for all 4096 combinations (12-bit pitch-class mask).
    // We keep a separate boolean array to indicate presence.
    private static readonly ChordInfo[] Lookup = new ChordInfo[4096];
    private static readonly bool[] HasChord = new bool[4096];

    // What third each quality has, read off the same interval templates below rather than
    // written out again, so a quality added there classifies itself here. Callers that ask
    // "major or minor?" of a chord kept their own list and left half the enum out of it.
    private static readonly ChordThird[] Thirds =
        new ChordThird[Enum.GetValues<ChordQuality>().Length];

    /// <summary>Note names indexed by pitch class (0=C .. 11=B), using sharp spellings.</summary>
    // IReadOnlyList so callers cannot mutate the shared table (indexing still works).
    public static IReadOnlyList<string> NoteNames { get; } = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    static ChordLibrary()
    {
        // Extended interval templates
        var templates = new (ChordQuality quality, int[] steps)[]
        {
            // Triads
            (ChordQuality.Major,      [0, 4, 7]),
            (ChordQuality.Minor,      [0, 3, 7]),
            (ChordQuality.Diminished, [0, 3, 6]),
            // NOTE: Augmented (and Diminished7 below) are fully symmetric: all rotations
            // of one chord share the SAME pitch-class set, so the mask lookup can only
            // ever answer the lowest registered root (C for {C,E,G#} etc.).
            // ChordAnalyzer.Identify re-roots them on the actual bass note.
            (ChordQuality.Augmented,  [0, 4, 8]),
            // NOTE: Sus2, Sus4 and Quartal are rotations of the SAME pitch-class set
            // ({r,r+2,r+7} == {r+7,r,r+2} as sus4 == {r+2,r+7,r+12} as quartal), so a bare
            // mask lookup can only ever return one of them — Sus2 wins by registration order.
            // ChordAnalyzer.Identify disambiguates using the actual bass note.
            (ChordQuality.Sus2,       [0, 2, 7]),
            (ChordQuality.Sus4,       [0, 5, 7]),

            // Power chord (dyad)
            (ChordQuality.Power,      [0, 7]),

            // Quartal harmony
            (ChordQuality.Quartal,    [0, 5, 10]),  // Stacked 4ths
            
            // Seventh chords
            (ChordQuality.Major7,     [0, 4, 7, 11]),
            (ChordQuality.Minor7,     [0, 3, 7, 10]),
            (ChordQuality.Dominant7,  [0, 4, 7, 10]),
            (ChordQuality.Dominant7Flat5, [0, 4, 6, 10]),
            (ChordQuality.Diminished7,[0, 3, 6, 9]),
            (ChordQuality.HalfDim7,   [0, 3, 6, 10]),
            (ChordQuality.MinorMajor7,[0, 3, 7, 11]),
            (ChordQuality.Augmented7, [0, 4, 8, 10]),
            
            // Add chords
            (ChordQuality.Add9,       [0, 4, 7, 14 % 12]), // 14 % 12 = 2
            (ChordQuality.Add11,      [0, 4, 7, 17 % 12]), // 17 % 12 = 5

            // Sixth chords. NOTE: {0,4,7,9} is a rotation of the minor seventh three semitones
            // below it ({C,E,G,A} == {A,C,E,G}) and {0,3,7,9} of the half-diminished seventh
            // ({C,Eb,G,A} == {A,C,Eb,G}), so these masks are already taken by Minor7 and HalfDim7
            // above and the registration below is a no-op for the lookup — deliberately, so a
            // bare mask lookup keeps answering what it always did. ChordAnalyzer.Identify uses
            // the actual bass to tell the two readings apart, as it does for sus and dim7. The
            // templates are still listed here because they are what ThirdOf reads.
            (ChordQuality.Major6,     [0, 4, 7, 9]),
            (ChordQuality.Minor6,     [0, 3, 7, 9]),
        };

        foreach (var (quality, steps) in templates)
        {
            Thirds[(int)quality] = Array.IndexOf(steps, 4) >= 0 ? ChordThird.Major
                : Array.IndexOf(steps, 3) >= 0 ? ChordThird.Minor
                : ChordThird.None;

            for (var root = 0; root < 12; root++)
            {
                ushort mask = 0;
                foreach (var step in steps)
                {
                    mask |= (ushort)(1 << ((root + step) % 12));
                }

                if (!HasChord[mask])
                {
                    Lookup[mask] = new ChordInfo((byte)root, quality);
                    HasChord[mask] = true;
                }
            }
        }
    }

    /// <summary>
    /// The third <paramref name="quality"/> is built on — the interval that decides whether a
    /// chord sounds major or minor, and <see cref="ChordThird.None"/> for the suspended, power
    /// and quartal chords, which state a root and leave the mode open.
    /// </summary>
    /// <remarks>
    /// <see cref="ChordQuality.Unknown"/> reports <see cref="ChordThird.None"/> as well, so a
    /// caller that treats "no third" as "no evidence of mode" must still check for Unknown when
    /// it means "no chord at all".
    /// </remarks>
    internal static ChordThird ThirdOf(ChordQuality quality)
    {
        var index = (int)quality;
        return (uint)index < (uint)Thirds.Length ? Thirds[index] : ChordThird.None;
    }

    /// <summary>
    /// Returns the chord for a 12-bit pitch-class mask, or an <c>Unknown</c>
    /// chord if the mask matches no known template.
    /// </summary>
    /// <remarks>
    /// A mask is a set of pitch classes and nothing else, so where several qualities share one
    /// set this can only answer whichever was registered first. It therefore never returns
    /// <see cref="ChordQuality.Sus4"/> or <see cref="ChordQuality.Quartal"/> (both share
    /// <see cref="ChordQuality.Sus2"/>'s set), nor <see cref="ChordQuality.Major6"/> or
    /// <see cref="ChordQuality.Minor6"/> (which share the sets of the sevenths a minor third
    /// below them), and it roots the symmetrical augmented and diminished-seventh chords on the
    /// lowest registered root rather than on the bass. Use
    /// <see cref="ChordAnalyzer.Identify(ReadOnlySpan{int})"/> to have the bass note decide, which
    /// is what tells those readings apart.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChordInfo GetChord(ushort mask)
    {
        return mask switch
        {
            // Mask is 12 bits (0-4095)
            >= 4096 => new ChordInfo(0, ChordQuality.Unknown),
            _ => HasChord[mask] ? Lookup[mask] : new ChordInfo(0, ChordQuality.Unknown)
        };
    }

    /// <summary>
    /// Tries to resolve a 12-bit pitch-class mask to a known chord.
    /// </summary>
    /// <param name="mask">12-bit pitch-class mask (0-4095).</param>
    /// <param name="chord">The matched chord, or an <c>Unknown</c> chord if none.</param>
    /// <returns><see langword="true"/> if a chord was found; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetChord(ushort mask, out ChordInfo chord)
    {
        if (mask < 4096 && HasChord[mask])
        {
            chord = Lookup[mask];
            return true;
        }
        chord = new ChordInfo(0, ChordQuality.Unknown);
        return false;
    }

    /// <summary>
    /// Get pitch class (0-11) from note name. Throws on unrecognized names
    /// instead of silently defaulting to C.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetPitchClass(string noteName)
    {
        ArgumentNullException.ThrowIfNull(noteName);
        return noteName.ToUpperInvariant() switch
        {
            "C" or "B#" => 0,
            "C#" or "DB" => 1,
            "D" => 2,
            "D#" or "EB" => 3,
            "E" or "FB" => 4,
            "F" or "E#" => 5,
            "F#" or "GB" => 6,
            "G" => 7,
            "G#" or "AB" => 8,
            "A" => 9,
            "A#" or "BB" => 10,
            "B" or "CB" => 11,
            _ => throw new ArgumentException($"Unrecognized note name: '{noteName}'", nameof(noteName))
        };
    }
}

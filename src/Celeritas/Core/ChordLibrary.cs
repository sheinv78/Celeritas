// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core;

/// <summary>
/// Recognized chord qualities.
/// </summary>
public enum ChordQuality : byte
{
    /// <summary>Unrecognized or unclassified chord.</summary>
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
    Dominant7Flat5
}

/// <summary>
/// Compact chord info (8 bytes instead of 24+ for class)
/// </summary>
/// <param name="RootPitchClass">Root pitch class of the chord (0=C .. 11=B).</param>
/// <param name="Quality">The chord's quality.</param>
public readonly record struct ChordInfo(byte RootPitchClass, ChordQuality Quality)
{
    /// <summary>Root note name (e.g. "C", "F#") for <see cref="RootPitchClass"/>.</summary>
    public string Root => ChordLibrary.NoteNames[RootPitchClass];

    /// <summary>Returns the root name followed by the quality (e.g. "C Major").</summary>
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
        };

        foreach (var (quality, steps) in templates)
        {
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
    /// Returns the chord for a 12-bit pitch-class mask, or an <c>Unknown</c>
    /// chord if the mask matches no known template.
    /// </summary>
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

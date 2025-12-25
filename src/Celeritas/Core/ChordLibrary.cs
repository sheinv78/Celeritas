// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core;

public enum ChordQuality : byte
{
    Unknown,
    Major,
    Minor,
    Diminished,
    Augmented,
    Major7,
    Minor7,
    Dominant7,
    Diminished7,
    HalfDim7,
    Sus2,
    Sus4,
    Power,          // 5th chord (no 3rd)
    Quartal,        // Built on 4ths
    Add9,
    Add11,
    MinorMajor7,
    Augmented7
}

/// <summary>
/// Compact chord info (8 bytes instead of 24+ for class)
/// </summary>
public readonly record struct ChordInfo(byte RootPitchClass, ChordQuality Quality)
{
    public string Root => ChordLibrary.NoteNames[RootPitchClass];
    public override string ToString() => $"{Root} {Quality}";
}

public static class ChordLibrary
{
    // Lookup array for all 4096 combinations (12-bit pitch-class mask).
    // We keep a separate boolean array to indicate presence.
    private static readonly ChordInfo[] Lookup = new ChordInfo[4096];
    private static readonly bool[] HasChord = new bool[4096];

    public static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    static ChordLibrary()
    {
        // Extended interval templates
        var templates = new (ChordQuality quality, int[] steps)[]
        {
            // Triads
            (ChordQuality.Major,      [0, 4, 7]),
            (ChordQuality.Minor,      [0, 3, 7]),
            (ChordQuality.Diminished, [0, 3, 6]),
            (ChordQuality.Augmented,  [0, 4, 8]),
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
            (ChordQuality.Diminished7,[0, 3, 6, 9]),
            (ChordQuality.HalfDim7,   [0, 3, 6, 10]),
            (ChordQuality.MinorMajor7,[0, 3, 7, 11]),
            (ChordQuality.Augmented7, [0, 4, 8, 10]),
            
            // Add chords
            (ChordQuality.Add9,       [0, 4, 7, 14 % 12]), // 14 % 12 = 2
            (ChordQuality.Add11,      [0, 4, 7, 17 % 12]), // 17 % 12 = 5
        };

        foreach (var t in templates)
        {
            for (var root = 0; root < 12; root++)
            {
                ushort mask = 0;
                foreach (var step in t.steps)
                {
                    mask |= (ushort)(1 << ((root + step) % 12));
                }

                if (!HasChord[mask])
                {
                    Lookup[mask] = new ChordInfo((byte)root, t.quality);
                    HasChord[mask] = true;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChordInfo GetChord(ushort mask)
    {
        // Mask is 12 bits (0-4095)
        if (mask >= 4096) return new ChordInfo(0, ChordQuality.Unknown);
        return HasChord[mask] ? Lookup[mask] : new ChordInfo(0, ChordQuality.Unknown);
    }

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
    /// Get pitch class (0-11) from note name
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetPitchClass(string noteName)
    {
        return noteName.ToUpperInvariant() switch
        {
            "C" => 0,
            "C#" or "DB" => 1,
            "D" => 2,
            "D#" or "EB" => 3,
            "E" => 4,
            "F" => 5,
            "F#" or "GB" => 6,
            "G" => 7,
            "G#" or "AB" => 8,
            "A" => 9,
            "A#" or "BB" => 10,
            "B" => 11,
            _ => 0
        };
    }
}

// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Church modes and modern modes derived from major scale.
/// </summary>
public enum Mode
{
    /// <summary>Major scale (W-W-H-W-W-W-H). Bright, happy.</summary>
    Ionian = 0,

    /// <summary>Minor with raised 6th (W-H-W-W-W-H-W). Jazz minor, melancholic but hopeful.</summary>
    Dorian = 1,

    /// <summary>Minor with lowered 2nd (H-W-W-W-H-W-W). Spanish, dark, exotic.</summary>
    Phrygian = 2,

    /// <summary>Major with raised 4th (W-W-W-H-W-W-H). Dreamy, floating, bright.</summary>
    Lydian = 3,

    /// <summary>Major with lowered 7th (W-W-H-W-W-H-W). Bluesy, rock, dominant sound.</summary>
    Mixolydian = 4,

    /// <summary>Natural minor (W-H-W-W-H-W-W). Sad, dark.</summary>
    Aeolian = 5,

    /// <summary>Diminished scale degree (H-W-W-H-W-W-W). Unstable, tense.</summary>
    Locrian = 6,

    // Extended modes

    /// <summary>Harmonic minor (W-H-W-W-H-A2-H). Classical, dramatic.</summary>
    HarmonicMinor = 7,

    /// <summary>Melodic minor ascending (W-H-W-W-W-W-H). Jazz, smooth.</summary>
    MelodicMinor = 8,

    /// <summary>Phrygian with major 3rd (H-A2-H-W-W-H-W). Flamenco, Spanish.</summary>
    PhrygianDominant = 9,

    /// <summary>Lydian with lowered 7th (W-W-W-H-W-H-W). Jazz fusion.</summary>
    LydianDominant = 10,

    /// <summary>Locrian with natural 2nd (W-H-W-H-W-W-W). Half-diminished chord scale.</summary>
    LocrianNatural2 = 11,

    /// <summary>Altered scale / Super Locrian (H-W-H-W-W-W-W). Dominant alt chord.</summary>
    Altered = 12,

    /// <summary>Whole tone scale (W-W-W-W-W-W). Dreamlike, impressionistic.</summary>
    WholeTone = 13,

    /// <summary>Diminished scale H-W pattern (H-W-H-W-H-W-H-W). Symmetric.</summary>
    DiminishedHalfWhole = 14,

    /// <summary>Diminished scale W-H pattern (W-H-W-H-W-H-W-H). Symmetric.</summary>
    DiminishedWholeHalf = 15,

    /// <summary>Blues scale (m3-W-H-H-m3-W). Blues, rock.</summary>
    Blues = 16,

    /// <summary>Major pentatonic (W-W-m3-W-m3). Folk, pop.</summary>
    MajorPentatonic = 17,

    /// <summary>Minor pentatonic (m3-W-W-m3-W). Rock, blues.</summary>
    MinorPentatonic = 18
}

/// <summary>
/// Extended key signature with modal information.
/// </summary>
public readonly struct ModalKey : IEquatable<ModalKey>
{
    /// <summary>Root note (0-11, where 0=C).</summary>
    public byte Root { get; }

    /// <summary>The mode/scale type.</summary>
    public Mode Mode { get; }

    public ModalKey(byte root, Mode mode)
    {
        Root = (byte)(root % 12);
        Mode = mode;
    }

    /// <summary>
    /// Convert from simple KeySignature.
    /// </summary>
    public static ModalKey FromKeySignature(KeySignature key)
        => new(key.Root, key.IsMajor ? Mode.Ionian : Mode.Aeolian);

    /// <summary>
    /// Convert to simple KeySignature (loses modal info for non-standard modes).
    /// </summary>
    public KeySignature ToKeySignature() => Mode switch
    {
        Mode.Ionian or Mode.Lydian or Mode.Mixolydian => new KeySignature(Root, true),
        Mode.Aeolian or Mode.Dorian or Mode.Phrygian or Mode.HarmonicMinor or Mode.MelodicMinor => new KeySignature(Root, false),
        Mode.Locrian => new KeySignature(Root, false),
        _ => new KeySignature(Root, true) // Default to major for exotic scales
    };

    /// <summary>
    /// Get the parallel major of this mode.
    /// </summary>
    public ModalKey ParallelMajor => new(Root, Mode.Ionian);

    /// <summary>
    /// Get the parallel minor of this mode.
    /// </summary>
    public ModalKey ParallelMinor => new(Root, Mode.Aeolian);

    /// <summary>
    /// Get the relative major (for minor modes).
    /// </summary>
    public ModalKey RelativeMajor => Mode switch
    {
        Mode.Aeolian => new((byte)((Root + 3) % 12), Mode.Ionian),
        Mode.Dorian => new((byte)((Root + 10) % 12), Mode.Ionian),
        Mode.Phrygian => new((byte)((Root + 8) % 12), Mode.Ionian),
        Mode.Locrian => new((byte)((Root + 1) % 12), Mode.Ionian),
        _ => new(Root, Mode.Ionian)
    };

    public override string ToString()
    {
        var noteName = ChordLibrary.NoteNames[Root];
        var modeName = Mode switch
        {
            Mode.Ionian => "Major",
            Mode.Aeolian => "Minor",
            Mode.HarmonicMinor => "Harmonic Minor",
            Mode.MelodicMinor => "Melodic Minor",
            _ => Mode.ToString()
        };
        return $"{noteName} {modeName}";
    }

    public bool Equals(ModalKey other) => Root == other.Root && Mode == other.Mode;
    public override bool Equals(object? obj) => obj is ModalKey other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Root, Mode);
    public static bool operator ==(ModalKey left, ModalKey right) => left.Equals(right);
    public static bool operator !=(ModalKey left, ModalKey right) => !left.Equals(right);
}

/// <summary>
/// Provides scale masks and mode analysis utilities.
/// </summary>
public static class ModeLibrary
{
    // Scale interval patterns (in semitones from root)
    private static readonly int[][] ModeIntervals =
    [
        [0, 2, 4, 5, 7, 9, 11],     // Ionian (Major)
        [0, 2, 3, 5, 7, 9, 10],     // Dorian
        [0, 1, 3, 5, 7, 8, 10],     // Phrygian
        [0, 2, 4, 6, 7, 9, 11],     // Lydian
        [0, 2, 4, 5, 7, 9, 10],     // Mixolydian
        [0, 2, 3, 5, 7, 8, 10],     // Aeolian (Natural Minor)
        [0, 1, 3, 5, 6, 8, 10],     // Locrian
        [0, 2, 3, 5, 7, 8, 11],     // Harmonic Minor
        [0, 2, 3, 5, 7, 9, 11],     // Melodic Minor (ascending)
        [0, 1, 4, 5, 7, 8, 10],     // Phrygian Dominant
        [0, 2, 4, 6, 7, 9, 10],     // Lydian Dominant
        [0, 2, 3, 5, 6, 8, 10],     // Locrian Natural 2
        [0, 1, 3, 4, 6, 8, 10],     // Altered (Super Locrian)
        [0, 2, 4, 6, 8, 10],        // Whole Tone (6 notes)
        [0, 1, 3, 4, 6, 7, 9, 10],  // Diminished H-W (8 notes)
        [0, 2, 3, 5, 6, 8, 9, 11],  // Diminished W-H (8 notes)
        [0, 3, 5, 6, 7, 10],        // Blues (6 notes)
        [0, 2, 4, 7, 9],            // Major Pentatonic (5 notes)
        [0, 3, 5, 7, 10]            // Minor Pentatonic (5 notes)
    ];

    /// <summary>
    /// Mode characteristics for musical description.
    /// </summary>
    public static readonly Dictionary<Mode, string> ModeCharacter = new()
    {
        [Mode.Ionian] = "bright, happy, resolved",
        [Mode.Dorian] = "minor but hopeful, jazzy, cool",
        [Mode.Phrygian] = "dark, Spanish, exotic, tense",
        [Mode.Lydian] = "dreamy, floating, ethereal, bright",
        [Mode.Mixolydian] = "bluesy, rock, dominant, groovy",
        [Mode.Aeolian] = "sad, dark, melancholic, natural minor",
        [Mode.Locrian] = "unstable, diminished, dissonant, rare",
        [Mode.HarmonicMinor] = "dramatic, classical, exotic, tense",
        [Mode.MelodicMinor] = "jazz, sophisticated, smooth, ascending",
        [Mode.PhrygianDominant] = "flamenco, Middle Eastern, exotic",
        [Mode.LydianDominant] = "jazz fusion, bright dominant, Simpsons theme",
        [Mode.LocrianNatural2] = "half-diminished, jazz minor ii",
        [Mode.Altered] = "tension, altered dominant, jazz climax",
        [Mode.WholeTone] = "dreamlike, impressionistic, floating",
        [Mode.DiminishedHalfWhole] = "symmetric, tense, diminished chords",
        [Mode.DiminishedWholeHalf] = "symmetric, dominant, jazz tension",
        [Mode.Blues] = "blues, expressive, vocal, bending",
        [Mode.MajorPentatonic] = "folk, optimistic, simple, universal",
        [Mode.MinorPentatonic] = "rock, blues, universal, guitar-friendly"
    };

    /// <summary>
    /// Get the 12-bit mask for a mode rooted on a given pitch.
    /// </summary>
    public static ushort GetScaleMask(ModalKey key)
    {
        var intervals = GetIntervals(key.Mode);
        ushort mask = 0;
        foreach (var interval in intervals)
        {
            mask |= (ushort)(1 << ((key.Root + interval) % 12));
        }
        return mask;
    }

    /// <summary>
    /// Get intervals for a mode.
    /// </summary>
    public static ReadOnlySpan<int> GetIntervals(Mode mode)
    {
        var index = (int)mode;
        return index < ModeIntervals.Length
            ? ModeIntervals[index]
            : ModeIntervals[0];
    }

    /// <summary>
    /// Get scale notes as pitch classes.
    /// </summary>
    public static int[] GetScaleNotes(ModalKey key)
    {
        var intervals = GetIntervals(key.Mode);
        var notes = new int[intervals.Length];
        for (int i = 0; i < intervals.Length; i++)
        {
            notes[i] = (key.Root + intervals[i]) % 12;
        }
        return notes;
    }

    /// <summary>
    /// Get note names for a scale.
    /// </summary>
    public static string[] GetScaleNoteNames(ModalKey key)
    {
        var notes = GetScaleNotes(key);
        var names = new string[notes.Length];
        for (int i = 0; i < notes.Length; i++)
        {
            names[i] = ChordLibrary.NoteNames[notes[i]];
        }
        return names;
    }

    /// <summary>
    /// Check if a pitch class belongs to a scale.
    /// </summary>
    public static bool ContainsPitch(ModalKey key, int pitchClass)
    {
        var mask = GetScaleMask(key);
        return (mask & (1 << (pitchClass % 12))) != 0;
    }

    /// <summary>
    /// Get the characteristic/avoid notes for a mode.
    /// Characteristic notes distinguish this mode from parallel major/minor.
    /// </summary>
    public static (int[] characteristic, int[] avoid) GetCharacteristicNotes(Mode mode)
    {
        return mode switch
        {
            Mode.Dorian => ([9], []),           // Raised 6th vs natural minor
            Mode.Phrygian => ([1], []),         // Lowered 2nd
            Mode.Lydian => ([6], []),           // Raised 4th
            Mode.Mixolydian => ([10], []),      // Lowered 7th vs major
            Mode.Locrian => ([1, 6], []),       // Lowered 2nd and 5th
            Mode.HarmonicMinor => ([11], []),   // Raised 7th vs natural minor
            Mode.MelodicMinor => ([9, 11], []), // Raised 6th and 7th
            Mode.PhrygianDominant => ([1, 4], []), // b2 and major 3rd
            Mode.LydianDominant => ([6, 10], []), // #4 and b7
            _ => ([], [])
        };
    }

    /// <summary>
    /// Detect the most likely mode from a pitch class distribution.
    /// </summary>
    public static (ModalKey key, float confidence) DetectMode(float[] distribution)
    {
        if (distribution.Length != 12)
            throw new ArgumentException("Distribution must have 12 elements", nameof(distribution));

        ModalKey bestKey = new(0, Mode.Ionian);
        float bestScore = float.MinValue;

        // Test all roots and common modes
        Mode[] modesToTest =
        [
            Mode.Ionian, Mode.Dorian, Mode.Phrygian, Mode.Lydian,
            Mode.Mixolydian, Mode.Aeolian, Mode.Locrian,
            Mode.HarmonicMinor, Mode.MelodicMinor
        ];

        // Find the most prominent note (likely the root)
        int likelyRoot = 0;
        float maxWeight = 0f;
        for (int i = 0; i < 12; i++)
        {
            if (distribution[i] > maxWeight)
            {
                maxWeight = distribution[i];
                likelyRoot = i;
            }
        }

        for (int root = 0; root < 12; root++)
        {
            foreach (var mode in modesToTest)
            {
                var testKey = new ModalKey((byte)root, mode);
                var score = ScoreAgainstMode(distribution, testKey);

                // Bonus for matching the most prominent note as root
                if (root == likelyRoot)
                {
                    score += 0.15f;
                }

                // Slight preference for common modes
                score += mode switch
                {
                    Mode.Ionian => 0.05f,
                    Mode.Aeolian => 0.04f,
                    Mode.Dorian => 0.03f,
                    Mode.Mixolydian => 0.02f,
                    Mode.Phrygian => 0.01f,
                    Mode.Lydian => 0.01f,
                    _ => 0f
                };

                if (score > bestScore)
                {
                    bestScore = score;
                    bestKey = testKey;
                }
            }
        }

        // Normalize confidence (0-1)
        var confidence = Math.Clamp((bestScore + 1f) / 2f, 0f, 1f);

        return (bestKey, confidence);
    }

    /// <summary>
    /// Detect mode with a hint about which note is the root.
    /// More accurate when the first note of a melody/scale is provided.
    /// </summary>
    public static (ModalKey key, float confidence) DetectModeWithRoot(float[] distribution, int rootHint)
    {
        if (distribution.Length != 12)
            throw new ArgumentException("Distribution must have 12 elements", nameof(distribution));

        rootHint = rootHint % 12;
        ModalKey bestKey = new((byte)rootHint, Mode.Ionian);
        float bestScore = float.MinValue;

        Mode[] modesToTest =
        [
            Mode.Ionian, Mode.Dorian, Mode.Phrygian, Mode.Lydian,
            Mode.Mixolydian, Mode.Aeolian, Mode.Locrian,
            Mode.HarmonicMinor, Mode.MelodicMinor
        ];

        // Only test with the hinted root
        foreach (var mode in modesToTest)
        {
            var testKey = new ModalKey((byte)rootHint, mode);
            var score = ScoreAgainstMode(distribution, testKey);

            if (score > bestScore)
            {
                bestScore = score;
                bestKey = testKey;
            }
        }

        var confidence = Math.Clamp((bestScore + 1f) / 2f, 0f, 1f);
        return (bestKey, confidence);
    }

    /// <summary>
    /// Detect mode from pitch classes with root hint.
    /// </summary>
    /// <param name="pitchClasses">Collection of pitch classes (0-11).</param>
    /// <param name="rootHint">Hint for the root note (pitch class).</param>
    public static (ModalKey key, float confidence) DetectModeWithRoot(IEnumerable<int> pitchClasses, int rootHint)
    {
        var distribution = new float[12];
        foreach (var pc in pitchClasses)
        {
            distribution[pc % 12] += 1f;
        }
        return DetectModeWithRoot(distribution, rootHint);
    }

    /// <summary>
    /// Detect mode from notes with root hint (automatically extracts pitch classes).
    /// </summary>
    /// <param name="notes">Collection of note events.</param>
    /// <param name="rootHint">Hint for the root note (pitch class). If null, uses first note's pitch class.</param>
    public static (ModalKey key, float confidence) DetectModeWithRoot(IEnumerable<NoteEvent> notes, int? rootHint = null)
    {
        var noteList = notes.ToList();
        if (noteList.Count == 0)
            throw new ArgumentException("Notes collection is empty", nameof(notes));

        var root = rootHint ?? (noteList[0].Pitch % 12);
        var distribution = new float[12];

        foreach (var note in noteList)
        {
            distribution[note.Pitch % 12] += 1f;
        }

        return DetectModeWithRoot(distribution, root);
    }

    private static float ScoreAgainstMode(float[] distribution, ModalKey key)
    {
        var intervals = GetIntervals(key.Mode);
        float inScale = 0f, outScale = 0f;
        float total = 0f;

        for (int i = 0; i < 12; i++)
        {
            total += distribution[i];
        }

        if (total == 0) return 0f;

        // Weight scale tones positively
        foreach (var interval in intervals)
        {
            var pc = (key.Root + interval) % 12;
            inScale += distribution[pc];
        }

        outScale = total - inScale;

        // Bonus for characteristic notes
        var (characteristic, _) = GetCharacteristicNotes(key.Mode);
        float charBonus = 0f;
        foreach (var c in characteristic)
        {
            var pc = (key.Root + c) % 12;
            charBonus += distribution[pc] * 0.2f;
        }

        return (inScale - outScale * 0.5f + charBonus) / total;
    }

    /// <summary>
    /// Get common chord types built on each scale degree for a mode.
    /// </summary>
    public static ChordQuality[] GetDiatonicChordQualities(Mode mode)
    {
        return mode switch
        {
            Mode.Ionian => [ChordQuality.Major, ChordQuality.Minor, ChordQuality.Minor,
                           ChordQuality.Major, ChordQuality.Major, ChordQuality.Minor, ChordQuality.Diminished],
            Mode.Dorian => [ChordQuality.Minor, ChordQuality.Minor, ChordQuality.Major,
                           ChordQuality.Major, ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Major],
            Mode.Phrygian => [ChordQuality.Minor, ChordQuality.Major, ChordQuality.Major,
                             ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Major, ChordQuality.Minor],
            Mode.Lydian => [ChordQuality.Major, ChordQuality.Major, ChordQuality.Minor,
                           ChordQuality.Diminished, ChordQuality.Major, ChordQuality.Minor, ChordQuality.Minor],
            Mode.Mixolydian => [ChordQuality.Major, ChordQuality.Minor, ChordQuality.Diminished,
                               ChordQuality.Major, ChordQuality.Minor, ChordQuality.Minor, ChordQuality.Major],
            Mode.Aeolian => [ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Major,
                            ChordQuality.Minor, ChordQuality.Minor, ChordQuality.Major, ChordQuality.Major],
            Mode.Locrian => [ChordQuality.Diminished, ChordQuality.Major, ChordQuality.Minor,
                            ChordQuality.Minor, ChordQuality.Major, ChordQuality.Major, ChordQuality.Minor],
            Mode.HarmonicMinor => [ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Augmented,
                                  ChordQuality.Minor, ChordQuality.Major, ChordQuality.Major, ChordQuality.Diminished],
            _ => [ChordQuality.Major, ChordQuality.Minor, ChordQuality.Minor,
                 ChordQuality.Major, ChordQuality.Major, ChordQuality.Minor, ChordQuality.Diminished]
        };
    }
}

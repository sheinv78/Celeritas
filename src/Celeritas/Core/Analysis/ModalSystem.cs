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

    /// <summary>Phrygian with major 3rd (H-A2-H-W-H-W-W). Flamenco, Spanish.</summary>
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
    /// <remarks>
    /// Never the answer of <see cref="ModeLibrary.DetectMode"/> or
    /// <see cref="ModeLibrary.DetectModeWithRoot(float[], int)"/>. Its five notes are contained
    /// in the major scale, so the scale is reported as a heptatonic mode that contains it, at
    /// confidence 0: Ionian on its own root when the root is hinted or is the most prominent
    /// note; which mode and root otherwise is documented on each method.
    /// </remarks>
    MajorPentatonic = 17,

    /// <summary>Minor pentatonic (m3-W-W-m3-W). Rock, blues.</summary>
    /// <remarks>
    /// Never the answer of <see cref="ModeLibrary.DetectMode"/> or
    /// <see cref="ModeLibrary.DetectModeWithRoot(float[], int)"/>. Its five notes are contained
    /// in Dorian, Phrygian and Aeolian on the same root and in three major keys, so the scale is
    /// reported as a heptatonic mode that contains it, at confidence 0: Aeolian on its own root
    /// when the root is hinted or is the most prominent note; which mode and root otherwise is
    /// documented on each method.
    /// </remarks>
    MinorPentatonic = 18
}

/// <summary>
/// Extended key signature with modal information.
/// </summary>
/// <remarks>
/// Validated on construction rather than at each consumer. ModeLibrary.GetIntervals and friends
/// take a ModalKey and guard the mode they read out of it, which would blame a parameter named
/// "mode" for a caller who only ever passed a "key". Rejecting it here puts the blame where the
/// bad value entered.
/// </remarks>
/// <exception cref="ArgumentOutOfRangeException">
/// <paramref name="mode"/> is not a defined <see cref="Analysis.Mode"/> value.
/// </exception>
public readonly struct ModalKey(byte root, Mode mode) : IEquatable<ModalKey>
{
    /// <summary>Root note (0-11, where 0=C).</summary>
    public byte Root { get; } = (byte)(root % 12);

    /// <summary>The mode/scale type.</summary>
    public Mode Mode { get; } = ThrowIfNotDefined(mode);

    private static Mode ThrowIfNotDefined(Mode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a defined Mode value.");

        return mode;
    }

    /// <summary>
    /// Convert from simple KeySignature.
    /// </summary>
    public static ModalKey FromKeySignature(KeySignature key)
        => new(key.Root, key.IsMajor ? Mode.Ionian : Mode.Aeolian);

    /// <summary>
    /// Convert to simple KeySignature (loses modal info for non-standard modes). A mode with a
    /// major third above its root is a major key; one whose only third is minor is a minor key.
    /// </summary>
    /// <remarks>
    /// The parity is read off the mode's own intervals rather than a list of modes. The list
    /// named the seven diatonic modes and the two minor scales and sent everything else to
    /// major, so the minor pentatonic, the blues scale, the whole-half diminished scale and
    /// Locrian natural 2 — the half-diminished scale — all came back as a major key though none
    /// of them has a major third. Where a scale has both thirds, as the altered and half-whole
    /// diminished scales do, the major third decides: they are dominant scales, whose minor
    /// third is heard as a sharp ninth over a chord whose own third is major. Every mode this
    /// library defines has one third or the other; a scale with neither would convert to major,
    /// which is where every unlisted mode went before, so no answer that was right has moved.
    /// </remarks>
    public KeySignature ToKeySignature()
    {
        var intervals = ModeLibrary.GetIntervals(Mode);
        var isMajor = intervals.Contains(MajorThird) || !intervals.Contains(MinorThird);
        return new KeySignature(Root, isMajor);
    }

    private const int MinorThird = 3;

    private const int MajorThird = 4;

    /// <summary>
    /// Get the parallel major of this mode.
    /// </summary>
    public ModalKey ParallelMajor => new(Root, Mode.Ionian);

    /// <summary>
    /// Get the parallel minor of this mode.
    /// </summary>
    public ModalKey ParallelMinor => new(Root, Mode.Aeolian);

    /// <summary>
    /// The major key built on the same notes as this mode — for Lydian on C, that is G major.
    /// </summary>
    /// <remarks>
    /// Every diatonic mode has one, not only the minor-sounding ones: the table listed Aeolian,
    /// Dorian, Phrygian and Locrian and let Lydian and Mixolydian fall through to the default,
    /// which returns the mode's own root. So C Lydian claimed C major as its relative major
    /// though the two differ by the F sharp that makes it Lydian, and the answer was silently
    /// the same as <see cref="ParallelMajor"/>. A mode with no diatonic parent — harmonic minor,
    /// whole tone, the pentatonics — still falls through to the parallel major, since there is no
    /// major scale on the same notes to point at.
    /// </remarks>
    public ModalKey RelativeMajor => Mode switch
    {
        Mode.Aeolian => new((byte)((Root + 3) % 12), Mode.Ionian),
        Mode.Dorian => new((byte)((Root + 10) % 12), Mode.Ionian),
        Mode.Phrygian => new((byte)((Root + 8) % 12), Mode.Ionian),
        Mode.Lydian => new((byte)((Root + 7) % 12), Mode.Ionian),
        Mode.Mixolydian => new((byte)((Root + 5) % 12), Mode.Ionian),
        Mode.Locrian => new((byte)((Root + 1) % 12), Mode.Ionian),
        _ => new(Root, Mode.Ionian)
    };

    /// <summary>
    /// Returns the key as a note name and mode (e.g. <c>"C Major"</c>), the tonic spelled as the
    /// scale is written — "Ab Major", "F Locrian", "Bb Blues" — never "G# Major" — and the mode
    /// by its musical name: "Phrygian Dominant", "Whole Tone", "Minor Pentatonic".
    /// </summary>
    /// <remarks>
    /// The modes past the four named ones printed their enum member — "D# MinorPentatonic",
    /// "E PhrygianDominant", "C DiminishedHalfWhole" — which is a C# identifier, not a name.
    /// </remarks>
    public override string ToString()
    {
        var noteName = KeySpelling.TonicName(this);
        var modeName = Mode switch
        {
            Mode.Ionian => "Major",
            Mode.Aeolian => "Minor",
            Mode.HarmonicMinor => "Harmonic Minor",
            Mode.MelodicMinor => "Melodic Minor",
            Mode.PhrygianDominant => "Phrygian Dominant",
            Mode.LydianDominant => "Lydian Dominant",
            Mode.LocrianNatural2 => "Locrian Natural 2",
            Mode.WholeTone => "Whole Tone",
            Mode.DiminishedHalfWhole => "Diminished (Half-Whole)",
            Mode.DiminishedWholeHalf => "Diminished (Whole-Half)",
            Mode.MajorPentatonic => "Major Pentatonic",
            Mode.MinorPentatonic => "Minor Pentatonic",
            _ => Mode.ToString()
        };
        return $"{noteName} {modeName}";
    }

    /// <summary>Indicates whether this key equals <paramref name="other"/> (same root and mode).</summary>
    public bool Equals(ModalKey other) => Root == other.Root && Mode == other.Mode;
    /// <summary>Indicates whether <paramref name="obj"/> is a <see cref="ModalKey"/> equal to this one.</summary>
    public override bool Equals(object? obj) => obj is ModalKey other && Equals(other);
    /// <summary>Returns a hash code combining root and mode.</summary>
    public override int GetHashCode() => HashCode.Combine(Root, Mode);
    /// <summary>Indicates whether two keys are equal.</summary>
    public static bool operator ==(ModalKey left, ModalKey right) => left.Equals(right);
    /// <summary>Indicates whether two keys differ.</summary>
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined <see cref="Mode"/> value.</exception>
    public static ReadOnlySpan<int> GetIntervals(Mode mode)
    {
        // The previous bounds test checked only the upper end, so an undefined mode fell back to
        // Ionian and a negative cast such as (Mode)(-1) reached ModeIntervals[-1]. Do not restore it.
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a defined Mode value.");

        return ModeIntervals[(int)mode];
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
    /// <remarks>
    /// <para>
    /// A heptatonic scale uses each of the seven letters A-G exactly once, so its notes are
    /// spelled out from the root's letter rather than read off the table of pitch-class names.
    /// Reading them off the table gave F sharp Ionian as "F# G# A# B C# D# F" — an F sharp and
    /// an F natural in one scale and no E at all, so that where the leading tone belongs a
    /// reader sees a diminished octave. Ninety-two of the hundred and fifty-six heptatonic mode
    /// and root pairs came back spelled that way.
    /// </para>
    /// <para>
    /// A root that has a letter of its own keeps it whenever the scale can be written from that
    /// letter: F Locrian is F Gb Ab Bb Cb Db Eb, not E# Locrian, though both need six
    /// accidentals — a mode is named by its final, and a chart of F Locrian that opens on E
    /// sharp tells the player the tonic is a raised E. Where a root sounds the same as two
    /// letters, the spelling that needs the fewest accidentals wins, which is what a musician
    /// writes: the Ionian mode on pitch class 8 is A flat major, not G sharp major with a
    /// double-sharped seventh. Ties between those go to the earlier letter, so pitch class 6 is
    /// spelled F sharp rather than G flat. The one heptatonic scale whose natural root cannot
    /// be written is F Altered, whose third degree would be B double-flat; it is spelled from E
    /// sharp.
    /// </para>
    /// <para>
    /// A scale that is not heptatonic — the pentatonics, the blues scale, whole tone, the
    /// diminished scales — has no letter-per-degree to keep, and its notes are named from the
    /// pitch-class table. So is a heptatonic scale that no spelling can write within single
    /// accidentals, since this library's note names carry at most one.
    /// </para>
    /// </remarks>
    public static string[] GetScaleNoteNames(ModalKey key)
    {
        var notes = GetScaleNotes(key);

        if (notes.Length == LetterCount && TrySpellByLetter(notes) is { } spelled)
        {
            return spelled;
        }

        var names = new string[notes.Length];
        for (int i = 0; i < notes.Length; i++)
        {
            names[i] = ChordLibrary.NoteNames[notes[i]];
        }
        return names;
    }

    private const int LetterCount = 7;

    private const string Letters = "CDEFGAB";

    /// <summary>The pitch class each letter names with no accidental on it.</summary>
    private static readonly int[] LetterPitchClasses = [0, 2, 4, 5, 7, 9, 11];

    /// <summary>
    /// The seven notes spelled one letter per degree, or <see langword="null"/> when no starting
    /// letter can write them all within a single accidental. A spelling whose root carries no
    /// accidental beats one whose root does; among the rest, fewer accidentals win, then the
    /// earlier letter.
    /// </summary>
    /// <remarks>
    /// The root's own letter used to count for nothing: with the accidentals tied at six, the
    /// earlier-letter rule spelled F Locrian from E sharp and B Lydian from C flat.
    /// </remarks>
    private static string[]? TrySpellByLetter(int[] notes)
    {
        string[]? best = null;
        var bestRootAltered = true;
        var fewestAccidentals = int.MaxValue;

        for (var startLetter = 0; startLetter < LetterCount; startLetter++)
        {
            var candidate = new string[LetterCount];
            var accidentals = 0;
            var writable = true;

            for (var degree = 0; degree < LetterCount; degree++)
            {
                var letter = (startLetter + degree) % LetterCount;
                var alteration = Centered(notes[degree] - LetterPitchClasses[letter]);

                if (alteration is < -1 or > 1)
                {
                    writable = false;
                    break;
                }

                accidentals += Math.Abs(alteration);
                candidate[degree] = Letters[letter] + alteration switch
                {
                    1 => "#",
                    -1 => "b",
                    _ => string.Empty
                };
            }

            if (!writable)
            {
                continue;
            }

            var rootAltered = notes[0] != LetterPitchClasses[startLetter];
            var beats = best is null
                || (!rootAltered && bestRootAltered)
                || (rootAltered == bestRootAltered && accidentals < fewestAccidentals);

            if (beats)
            {
                bestRootAltered = rootAltered;
                fewestAccidentals = accidentals;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// A distance in semitones brought into -6..5, so that eleven semitones up reads as one
    /// semitone down — which is what an accidental on the letter above means.
    /// </summary>
    private static int Centered(int semitones)
    {
        var folded = PitchMath.Fold(semitones);
        return folded > 6 ? folded - 12 : folded;
    }

    /// <summary>
    /// Check if a pitch class belongs to a scale. A <paramref name="pitchClass"/> outside 0-11 is
    /// folded to its pitch class rather than rejected.
    /// </summary>
    public static bool ContainsPitch(ModalKey key, int pitchClass)
    {
        var mask = GetScaleMask(key);
        // Fold, not `%`: `%` keeps the sign in C#, so a negative pitch class shifted by a
        // negative amount (1 << -n sets bit 31) and the test was always false.
        return (mask & (1 << PitchMath.Fold(pitchClass))) != 0;
    }

    /// <summary>
    /// Get the characteristic and avoid notes of a mode, as semitones above its root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A characteristic note is the degree that tells this mode from the parallel major or
    /// minor: the raised 6th of Dorian, the lowered 2nd of Phrygian, the raised 4th of Lydian.
    /// Ionian and Aeolian have none, because they are what the others are compared against, and
    /// the list is empty for the scales that are not compared this way at all — the pentatonics,
    /// the blues scale, and the symmetrical whole-tone and diminished scales.
    /// </para>
    /// <para>
    /// An avoid note is a degree a semitone above a note of the mode's own tonic seventh chord:
    /// it sounds against that chord rather than with it, so it is passed through rather than
    /// rested on. This half of the answer used to be empty for all nineteen modes, so a caller
    /// asking the question got nothing back for it whatever the mode. It is worked out from the
    /// scale rather than listed, which means a mode added later answers too, and it comes out
    /// where the textbooks put it: the 4th in Ionian and Mixolydian, the flat 6th in Aeolian,
    /// the flat 2nd in Locrian — and nothing at all in Lydian and Dorian, which is exactly why
    /// those two are the modes a melody can move through freely.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined <see cref="Mode"/> value.</exception>
    public static (int[] characteristic, int[] avoid) GetCharacteristicNotes(Mode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a defined Mode value.");

        int[] characteristic = mode switch
        {
            Mode.Dorian => [9],           // Raised 6th vs natural minor
            Mode.Phrygian => [1],         // Lowered 2nd
            Mode.Lydian => [6],           // Raised 4th
            Mode.Mixolydian => [10],      // Lowered 7th vs major
            Mode.Locrian => [1, 6],       // Lowered 2nd and 5th
            Mode.HarmonicMinor => [11],   // Raised 7th vs natural minor
            Mode.MelodicMinor => [9, 11], // Raised 6th and 7th
            Mode.PhrygianDominant => [1, 4], // b2 and major 3rd
            Mode.LydianDominant => [6, 10], // #4 and b7
            _ => []
        };

        return (characteristic, AvoidNotes(mode));
    }

    /// <summary>
    /// The degrees of <paramref name="mode"/> that sit a semitone above a note of its own tonic
    /// seventh chord, as semitones above the root.
    /// </summary>
    /// <remarks>
    /// The tonic seventh is degrees 1, 3, 5 and 7 of the scale, which only a seven-note scale
    /// has; a mode with any other number of notes has no avoid note to report here.
    /// </remarks>
    private static int[] AvoidNotes(Mode mode)
    {
        var intervals = GetIntervals(mode);
        if (intervals.Length != LetterCount)
        {
            return [];
        }

        var chord = 0;
        for (var degree = 0; degree < LetterCount; degree += 2)
        {
            chord |= 1 << intervals[degree];
        }

        var avoid = new List<int>(2);
        foreach (var interval in intervals)
        {
            // A chord tone is never its own avoid note, and the root is a chord tone, so the
            // semitone below the root — the leading tone of a harmonic-minor scale, say — is
            // caught by the same test without a special case.
            if ((chord & (1 << interval)) != 0)
            {
                continue;
            }

            if ((chord & (1 << PitchMath.Fold(interval - 1))) != 0)
            {
                avoid.Add(interval);
            }
        }

        return [.. avoid];
    }

    /// <summary>
    /// Detect the most likely mode from a pitch class distribution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The answer is never <see cref="Mode.MajorPentatonic"/> or <see cref="Mode.MinorPentatonic"/>.
    /// Each is contained in seven-note modes that are candidates, and the score rewards
    /// containing the notes played, so a contained scale can only tie with its container: a
    /// pentatonic is reported as a heptatonic mode that contains it, at confidence 0 — the
    /// margin that says several modes fit equally. Which mode depends on whether a note stands
    /// out. When the pentatonic's own root is the most prominent note, a major pentatonic comes
    /// back as Ionian on that root and a minor pentatonic as Aeolian on it. When no note stands
    /// out — each of the five played equally — both come back as Ionian, on the one of the three
    /// major keys that contain the five notes that the tie-break below points to: for a major
    /// pentatonic that is its own tonic, so C major pentatonic is C major and G major pentatonic
    /// is G major; for a minor pentatonic it is the relative major, so A minor pentatonic is
    /// C major and C minor pentatonic is E flat major.
    /// </para>
    /// <para>
    /// On each root the mode is the one <see cref="DetectModeWithRoot(float[], int)"/> names there,
    /// so asking again with the detected root gives the same answer; the preferences for a
    /// prominent root and for the common modes then decide between roots. When roots still tie —
    /// six notes of an octatonic scale fit four half-whole and four whole-half roots exactly, and
    /// the tonic, third, fifth, sixth and seventh of a melodic minor also fit the harmonic minor a
    /// major third up — the tie is broken by the distribution itself: the root carrying the most
    /// weight, then the root nearest above the heaviest pitch class, then the root from which the
    /// weights read heaviest-first, and only then the order of the modes. Each of those moves with
    /// the music, so a transposed passage is answered in the transposed key with the same mode and
    /// the same confidence.
    /// </para>
    /// <para>
    /// The tie used to go to the lowest-numbered root, and the confidence was measured on it: the
    /// same octatonic lick read as C# half-whole in one key, as C half-whole a whole tone higher
    /// and as C whole-half, at twice the confidence, a major third higher; and a cell that read as
    /// C melodic minor read as C harmonic minor eight semitones up. Confidence is the margin among
    /// modes on the chosen root, not "how well it fits": a single note fits many modes, so a
    /// fit-based score reported false certainty (#30).
    /// The common-mode preference also used to be added to every mode on every root, so it picked
    /// between modes on one root as well: a set that fit Lydian a little better was still named
    /// Ionian, and the confidence — that margin — then described Lydian's lead over the mode
    /// actually named, while <see cref="DetectModeWithRoot(float[], int)"/> on the same root said
    /// Lydian.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="distribution"/> is <see langword="null"/>.</exception>
    public static (ModalKey key, float confidence) DetectMode(float[] distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);

        if (distribution.Length != 12)
            throw new ArgumentException("Distribution must have 12 elements", nameof(distribution));

        if (!HasWeight(distribution))
            return (new ModalKey(0, Mode.Ionian), 0f);

        // Find the most prominent note (likely the root). When several notes carry the same top
        // weight there is no most prominent note, and awarding the bonus below to the
        // lowest-numbered of them made the answer depend on pitch-class numbering instead of on
        // the music: a plain major scale came back as Ionian written in C, Locrian written in
        // C#, Aeolian in D# and Lydian in G, because every rotation still contains pitch class 0
        // and 0 always won the tie. -1 means "nothing stands out", which no root matches.
        int likelyRoot = -1;
        float maxWeight = 0f;
        var sharedTopWeight = false;
        for (int i = 0; i < 12; i++)
        {
            if (distribution[i] > maxWeight)
            {
                maxWeight = distribution[i];
                likelyRoot = i;
                sharedTopWeight = false;
            }
            else if (likelyRoot >= 0 && distribution[i] == maxWeight)
            {
                sharedTopWeight = true;
            }
        }

        if (sharedTopWeight)
        {
            likelyRoot = -1;
        }

        // Every pitch class carrying the top weight, for the tie-break below; the prominent-root
        // bonus stands down when the top weight is shared, but the tie-break can still use it.
        var heaviest = 0;
        for (int i = 0; i < 12; i++)
        {
            if (distribution[i] == maxWeight)
            {
                heaviest |= 1 << i;
            }
        }

        // One candidate per root: the mode DetectModeWithRoot would name there, scored with the
        // preferences that decide between roots.
        Span<Mode> modes = stackalloc Mode[12];
        Span<float> scores = stackalloc float[12];
        float bestScore = float.MinValue;

        for (int root = 0; root < 12; root++)
        {
            var (mode, score) = BestModeOnRoot(distribution, root);

            // Bonus for matching the most prominent note as root
            if (root == likelyRoot)
            {
                score += 0.15f;
            }

            // Slight preference for common modes. With no root hint, every rotation of a
            // scale fits its notes exactly, so this is what decides which of them to name —
            // and thereby where the root is. Harmonic and melodic minor need an entry for
            // the same reason Ionian does: they are the ordinary name for their rotations.
            // Without one, F harmonic minor tied with C Phrygian Dominant (its fifth mode)
            // and A melodic minor with C altered (its seventh), both were settled by the
            // order of the root loop, and the answer stopped following the music: every
            // transposition of the scale came back rooted on pitch class 0.
            score += CommonModePreference(mode);

            modes[root] = mode;
            scores[root] = score;
            if (score > bestScore)
            {
                bestScore = score;
            }
        }

        // Every root within epsilon of the best is a candidate, and the distribution — not the
        // root's number — decides between them. The first root within reach is the incumbent, so
        // the lowest number wins only when nothing about the music tells the roots apart.
        var chosen = -1;
        for (int root = 0; root < 12; root++)
        {
            if (scores[root] < bestScore - ScoreEpsilon)
            {
                continue;
            }

            if (chosen < 0 || PrefersRoot(distribution, heaviest, root, modes[root], chosen, modes[chosen]))
            {
                chosen = root;
            }
        }

        var bestKey = new ModalKey((byte)chosen, modes[chosen]);

        // Confidence is the margin among modes on the chosen root, not "how well it fits":
        // a single note fits many modes, so a fit-based score reported false certainty (#30).
        var confidence = ModeMargin(distribution, chosen);

        return (bestKey, confidence);
    }

    /// <summary>
    /// Detect mode with a hint about which note is the root.
    /// More accurate when the first note of a melody/scale is provided.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The answer is never <see cref="Mode.MajorPentatonic"/> or <see cref="Mode.MinorPentatonic"/>.
    /// Each is contained in seven-note modes on the same root that are candidates, and the score
    /// rewards containing the notes played, so a contained scale can only tie with its container.
    /// A major pentatonic on the hinted root comes back as Ionian on that root, and a minor
    /// pentatonic as Aeolian on it — the most common of Dorian, Phrygian and Aeolian, which the
    /// five notes do not tell apart — both at confidence 0, the margin that says so.
    /// </para>
    /// <para>
    /// Modes that fit the hinted root equally — a minor pentatonic fits Aeolian, Dorian, Phrygian
    /// and the blues scale alike — are settled by the same preference for the common modes that
    /// <see cref="DetectMode"/> applies, so the two name the same mode on the same root. The first
    /// in the candidate list used to win here: that pentatonic with its root stressed was C Dorian
    /// from this overload and C minor from <see cref="DetectMode"/>, so the two could disagree about
    /// the very root one of them had just detected.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="distribution"/> is <see langword="null"/>.</exception>
    public static (ModalKey key, float confidence) DetectModeWithRoot(float[] distribution, int rootHint)
    {
        ArgumentNullException.ThrowIfNull(distribution);

        if (distribution.Length != 12)
            throw new ArgumentException("Distribution must have 12 elements", nameof(distribution));

        // A bare `% 12` keeps the sign, and the (byte) cast below then wraps it instead of
        // failing: rootHint -1 became byte 255, which scores as pitch class 3. A caller hinting
        // one semitone below C was answered in D#, with the confidence of a real detection.
        rootHint = PitchMath.Fold(rootHint);

        if (!HasWeight(distribution))
            return (new ModalKey((byte)rootHint, Mode.Ionian), 0f);

        // Only test with the hinted root
        var (mode, _) = BestModeOnRoot(distribution, rootHint);

        var confidence = ModeMargin(distribution, rootHint);
        return (new ModalKey((byte)rootHint, mode), confidence);
    }

    /// <summary>
    /// A score difference smaller than this is a tie. Scores are sums of a dozen floats, so the same
    /// set of notes summed in a different order — a half-whole scale and the whole-half scale a
    /// semitone up — can differ in the last digit without differing in the music.
    /// </summary>
    private const float ScoreEpsilon = 1e-4f;

    /// <summary>
    /// The mode that fits <paramref name="distribution"/> best on <paramref name="root"/>, and its
    /// raw score. Both detection overloads name a root's mode through this, which is what makes them
    /// agree.
    /// </summary>
    /// <remarks>
    /// Modes that tie are settled by <see cref="CommonModePreference"/> and then by the order of
    /// <see cref="DetectableModes"/>: the more common reading of an ambiguous set is the better
    /// answer, and it is the one <see cref="DetectMode"/> already gave.
    /// </remarks>
    private static (Mode mode, float score) BestModeOnRoot(float[] distribution, int root)
    {
        Span<float> scores = stackalloc float[DetectableModes.Length];
        float bestScore = float.MinValue;
        for (int i = 0; i < DetectableModes.Length; i++)
        {
            scores[i] = ScoreAgainstMode(distribution, new ModalKey((byte)root, DetectableModes[i]));
            if (scores[i] > bestScore)
            {
                bestScore = scores[i];
            }
        }

        var best = -1;
        for (int i = 0; i < DetectableModes.Length; i++)
        {
            if (scores[i] < bestScore - ScoreEpsilon)
            {
                continue;
            }

            if (best < 0 || CommonModePreference(DetectableModes[i]) > CommonModePreference(DetectableModes[best]))
            {
                best = i;
            }
        }

        return (DetectableModes[best], scores[best]);
    }

    /// <summary>
    /// How much more readily <paramref name="mode"/> is named than an equally fitting rarer one:
    /// the ordinary names first, their rotations after.
    /// </summary>
    private static float CommonModePreference(Mode mode) => mode switch
    {
        Mode.Ionian => 0.05f,
        Mode.Aeolian => 0.04f,
        Mode.Dorian => 0.03f,
        Mode.Mixolydian => 0.02f,
        Mode.Phrygian => 0.01f,
        Mode.Lydian => 0.01f,
        Mode.HarmonicMinor => 0.01f,
        Mode.MelodicMinor => 0.01f,
        _ => 0f
    };

    /// <summary>
    /// Whether <paramref name="candidate"/> is the better root than <paramref name="incumbent"/> for
    /// a distribution that fits both equally well, judged by the distribution alone.
    /// </summary>
    /// <remarks>
    /// Every test here is about where the weight lies relative to the root, never about the root's
    /// number, so the answer transposes with the music. The last resort — the earlier mode, and
    /// failing that the incumbent — is reached only when the weights read identically from both
    /// roots, which means the distribution repeats at that interval (a whole-tone scale, an
    /// augmented triad) and the two roots are the same music.
    /// </remarks>
    private static bool PrefersRoot(
        float[] distribution, int heaviest, int candidate, Mode candidateMode, int incumbent, Mode incumbentMode)
    {
        // The root that carries the most weight.
        if (distribution[candidate] != distribution[incumbent])
        {
            return distribution[candidate] > distribution[incumbent];
        }

        // The root nearest above the heaviest pitch class: a prominent note that is not the root
        // is most often the leading tone, resolving up to it.
        var candidateDistance = DistanceAboveHeaviest(candidate, heaviest);
        var incumbentDistance = DistanceAboveHeaviest(incumbent, heaviest);
        if (candidateDistance != incumbentDistance)
        {
            return candidateDistance < incumbentDistance;
        }

        // The root from which the weights read heaviest-first: the start of a scalar run.
        for (int i = 1; i < 12; i++)
        {
            var fromCandidate = distribution[(candidate + i) % 12];
            var fromIncumbent = distribution[(incumbent + i) % 12];
            if (fromCandidate != fromIncumbent)
            {
                return fromCandidate > fromIncumbent;
            }
        }

        return Array.IndexOf(DetectableModes, candidateMode) < Array.IndexOf(DetectableModes, incumbentMode);
    }

    /// <summary>
    /// How many semitones <paramref name="root"/> sits above the nearest pitch class in the
    /// <paramref name="heaviest"/> mask below it: 0 when the root is itself one of them.
    /// </summary>
    private static int DistanceAboveHeaviest(int root, int heaviest)
    {
        for (int distance = 0; distance < 12; distance++)
        {
            if ((heaviest & (1 << PitchMath.Fold(root - distance))) != 0)
            {
                return distance;
            }
        }

        return 12;
    }

    /// <summary>
    /// Detect mode from pitch classes with root hint.
    /// </summary>
    /// <remarks>
    /// A pentatonic is answered as <see cref="DetectModeWithRoot(float[], int)"/> documents: as
    /// a heptatonic mode on the hinted root that contains it, at confidence 0.
    /// </remarks>
    /// <param name="pitchClasses">Collection of pitch classes; values outside 0-11 are folded to
    /// their pitch class rather than rejected.</param>
    /// <param name="rootHint">Hint for the root note (pitch class).</param>
    /// <exception cref="ArgumentNullException"><paramref name="pitchClasses"/> is <see langword="null"/>.</exception>
    public static (ModalKey key, float confidence) DetectModeWithRoot(IEnumerable<int> pitchClasses, int rootHint)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        var distribution = new float[12];
        foreach (var pc in pitchClasses)
        {
            // Fold like the sibling overloads: `%` keeps the sign, so a negative pitch
            // class indexed backwards out of the distribution.
            distribution[PitchMath.Fold(pc)] += 1f;
        }
        return DetectModeWithRoot(distribution, rootHint);
    }

    /// <summary>
    /// Detect mode from notes with root hint (automatically extracts pitch classes).
    /// </summary>
    /// <remarks>
    /// A pentatonic is answered as <see cref="DetectModeWithRoot(float[], int)"/> documents: as
    /// a heptatonic mode on the root that contains it, at confidence 0.
    /// </remarks>
    /// <param name="notes">Collection of note events. Rests are silence and do not count towards
    /// the mode, nor can one be the root.</param>
    /// <param name="rootHint">Hint for the root note (pitch class). If null, uses the first
    /// sounding note's pitch class.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="notes"/> holds nothing that sounds —
    /// it is empty, or every event in it is a rest.</exception>
    public static (ModalKey key, float confidence) DetectModeWithRoot(IEnumerable<NoteEvent> notes, int? rootHint = null)
    {
        // ToList() throws on null, but names its own "source" parameter instead of this one.
        ArgumentNullException.ThrowIfNull(notes);

        // Rests are silence, not a pitch class. Folding RestPitch (-1) put a B nobody played into
        // the distribution, and that phantom B is precisely the degree that names the wrong mode:
        // the leading tone Mixolydian does not have, the raised 7th that turns Aeolian into
        // harmonic minor and Dorian into melodic minor, the #4 that turns Ionian into Lydian. The
        // root was read off it too, so a passage opening with a rest was answered in B.
        var noteList = notes.Where(note => !Rests.IsRest(note.Pitch)).ToList();
        if (noteList.Count == 0)
            throw new ArgumentException("Notes collection holds nothing that sounds", nameof(notes));

        // Both folds matter: `%` keeps the sign, so a pitch below zero gave a negative root and
        // indexed backwards out of the distribution.
        var root = rootHint ?? PitchMath.Fold(noteList[0].Pitch);
        var distribution = new float[12];

        foreach (var note in noteList)
        {
            distribution[PitchMath.Fold(note.Pitch)] += 1f;
        }

        return DetectModeWithRoot(distribution, root);
    }

    /// <summary>
    /// Whether a distribution carries any positive weight to detect a mode from.
    /// </summary>
    /// <remarks>
    /// An empty distribution is not merely inconclusive — it is actively misreported. Every mode
    /// ties at a structural score of 0 (see the <c>total == 0</c> guard in ScoreAgainstMode), and
    /// then the prominent-root (+0.15) and common-mode (+0.05) tie-breakers lift the winner to
    /// ~0.2, which the confidence formula turns into 0.6. So the caller was told "C Ionian, 60%
    /// confident" about silence. KeyProfiler.Detect reports zero confidence in exactly this case
    /// ("when the best correlation is not positive the ratio is meaningless"); this agrees with it.
    /// The bar is a positive sum, not a nonzero one, so a distribution that cancels to zero — or is
    /// entirely non-positive — is caught too, rather than dividing by it further down.
    /// </remarks>
    private static bool HasWeight(float[] distribution)
    {
        float total = 0f;
        foreach (var weight in distribution)
        {
            total += weight;
        }

        return total > 0f;
    }

    /// <summary>
    /// The modes detection chooses between. Every <see cref="Mode"/> except the two pentatonics,
    /// which are proper subsets of modes already here.
    /// </summary>
    /// <remarks>
    /// The list held only the seven diatonic modes plus harmonic and melodic minor, so eight of
    /// the modes this library defines could never be named: the exact notes of C Phrygian
    /// Dominant came back as C Phrygian — a mode without the major third that defines the scale —
    /// at confidence 0.272, which for this detector is a confident answer, and the altered scale
    /// likewise came back as Locrian. Widening the list corrects 96 of 228 mode/root pairs and
    /// changes no answer that was already right: not one full scale moves to a different wrong
    /// mode, and not one of the 105 five-note melodies drawn from a diatonic mode changes at all.
    ///
    /// The pentatonics stay out because the score rewards containing the notes played, so a scale
    /// contained in another can only tie with it, never win — a minor pentatonic fits Aeolian and
    /// Dorian perfectly and adding MinorPentatonic would just make it a three-way tie decided by
    /// the order of this array. A pentatonic melody is reported as its containing mode with a
    /// confidence of zero, which says the same thing more honestly.
    /// </remarks>
    private static readonly Mode[] DetectableModes =
    [
        Mode.Ionian, Mode.Dorian, Mode.Phrygian, Mode.Lydian,
        Mode.Mixolydian, Mode.Aeolian, Mode.Locrian,
        Mode.HarmonicMinor, Mode.MelodicMinor,
        Mode.PhrygianDominant, Mode.LydianDominant, Mode.LocrianNatural2, Mode.Altered,
        Mode.WholeTone, Mode.DiminishedHalfWhole, Mode.DiminishedWholeHalf, Mode.Blues
    ];

    /// <summary>
    /// Confidence that mode detection on a fixed <paramref name="root"/> is decisive: the margin
    /// between the best-fitting mode and the next-best <em>different</em> mode on that root, using
    /// the same <c>(best - second) / (best + eps)</c> ratio as <see cref="KeyProfiler"/>.
    /// </summary>
    /// <remarks>
    /// The old <c>(bestScore + 1) / 2</c> reported near-total confidence whenever the notes simply
    /// <em>fit</em> the winning mode — but a single pitch class fits every mode that contains it, so
    /// one note detected an exotic mode at 100%. A margin answers the real question: not "do these
    /// notes fit?" but "do they fit this mode better than the alternatives on the same root?". A
    /// single note, a bare triad, or a pentatonic that omits the distinguishing degrees leaves the
    /// top modes tied, so the margin — and the confidence — is ~0; a full scale separates its mode
    /// and the margin is positive. Candidates are scored <em>raw</em> here, without the prominent-root
    /// and common-mode tie-breakers that pick <em>which</em> answer to return, so the number reflects
    /// the data's fit rather than the heuristics. Restricting the comparison to a single root is
    /// deliberate: relative modes (C Ionian and A Aeolian) share every pitch class, so a margin taken
    /// across all roots would collapse to ~0 even for an unambiguous scale — root disambiguation is a
    /// separate axis, handled by the prominent-note heuristic, not by this score.
    /// </remarks>
    private static float ModeMargin(float[] distribution, int root)
    {
        float best = float.MinValue, second = float.MinValue;
        foreach (var mode in DetectableModes)
        {
            var score = ScoreAgainstMode(distribution, new ModalKey((byte)root, mode));
            if (score > best)
            {
                second = best;
                best = score;
            }
            else if (score > second)
            {
                second = score;
            }
        }

        return best > 0f ? Math.Clamp((best - second) / (best + 0.001f), 0f, 1f) : 0f;
    }

    private static float ScoreAgainstMode(float[] distribution, ModalKey key)
    {
        var intervals = GetIntervals(key.Mode);
        float inScale = 0f;
        float total = 0f;

        // Summed from the root, like inScale below, so that a transposed distribution scores
        // bit-for-bit the same. Float addition is not associative: summed from pitch class 0,
        // the same music reported a confidence that differed in its last digits from key to key.
        for (int i = 0; i < 12; i++)
        {
            total += distribution[(key.Root + i) % 12];
        }

        if (total == 0) return 0f;

        // Weight scale tones positively
        foreach (var interval in intervals)
        {
            var pc = (key.Root + interval) % 12;
            inScale += distribution[pc];
        }

        var outScale = total - inScale;

        // No bonus for characteristic notes. A characteristic note is a scale tone, so it is
        // already counted in inScale, and the fit difference it is meant to capture is carried
        // there far more strongly: the raised sixth that separates Dorian from Aeolian is worth
        // 1 inside one scale and -0.5 outside the other. Added again at 0.2 it did nothing where
        // it was meant to help and quietly decided the answer where it was not — the table has
        // no entry for Ionian or Aeolian, which by its own definition are what the other modes
        // are characteristic AGAINST, so a bare diatonic scale, which fits every rotation
        // equally, could never be read as major or minor. It came back as Dorian on the second
        // degree, in every key.
        return (inScale - (outScale * 0.5f)) / total;
    }

    /// <summary>
    /// Get common chord types built on each scale degree for a mode.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined <see cref="Mode"/> value.</exception>
    public static ChordQuality[] GetDiatonicChordQualities(Mode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a defined Mode value.");

        // Built from the mode's own scale rather than read from a table. The table listed eight
        // modes and answered the major scale's qualities for everything else, so the whole-tone
        // scale — every triad in which is augmented — reported I major, ii minor, iii minor, and
        // the two pentatonic scales, which have five degrees, reported seven chords. Stacking
        // thirds out of the scale gives the same answers as the table did for the modes it knew,
        // one per degree the scale actually has.
        var intervals = GetIntervals(mode);
        var qualities = new ChordQuality[intervals.Length];

        for (var degree = 0; degree < intervals.Length; degree++)
        {
            var root = intervals[degree];
            var third = PitchMath.Fold(intervals[(degree + 2) % intervals.Length] - root);
            var fifth = PitchMath.Fold(intervals[(degree + 4) % intervals.Length] - root);

            qualities[degree] = (third, fifth) switch
            {
                (4, 7) => ChordQuality.Major,
                (3, 7) => ChordQuality.Minor,
                (3, 6) => ChordQuality.Diminished,
                (4, 8) => ChordQuality.Augmented,
                (2, 7) => ChordQuality.Sus2,
                (5, 7) => ChordQuality.Sus4,
                // A scale whose degrees do not stack into a triad — the pentatonics — has no
                // chord to name here, and saying so beats naming one it does not contain.
                _ => ChordQuality.Unknown
            };
        }

        return qualities;
    }
}

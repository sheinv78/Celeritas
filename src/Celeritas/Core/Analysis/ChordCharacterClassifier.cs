// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Lightweight chord character classification for single chord symbols (example-friendly API).
/// This does not require a key/context; it maps chord quality to a coarse character and metrics.
/// </summary>
public static class ChordCharacterClassifier
{
    /// <exception cref="ArgumentNullException"><paramref name="chordSymbol"/> is <see langword="null"/>.</exception>
    public static ChordCharacterClassification Classify(string chordSymbol)
    {
        // Null used to reach the IsNullOrWhiteSpace branch and come back as Unknown; the guard
        // also stays outside the try below, whose blanket catch would swallow the throw.
        ArgumentNullException.ThrowIfNull(chordSymbol);

        if (string.IsNullOrWhiteSpace(chordSymbol))
            return ChordCharacterClassification.Unknown;

        try
        {
            // The symbol names its root and the quality follows from the intervals above it:
            // "Csus4" is a Sus4 and "Am7/C" a Minor7, whatever the bass. An unparsable symbol
            // must not fall through to a zero mask, which the quality switch's default arm
            // would report as a maximally stable chord instead of Unknown.
            if (ParsedChord.FromSymbol(chordSymbol.Trim()) is not { } chord)
            {
                return ChordCharacterClassification.Unknown;
            }

            return FromQuality(chord.Info.Quality);
        }
        catch
        {
            return ChordCharacterClassification.Unknown;
        }
    }

    private static ChordCharacterClassification FromQuality(ChordQuality quality)
    {
        // A quality this table does not name has no character to report, and saying so is the
        // only honest answer. The arm below used to be `_ => Stable`, whose 0.90 is the highest
        // stability in the table: a 7b5 — an altered dominant, about as unsettled as tonal
        // harmony gets — came back steadier than a major triad, and so would every quality added
        // to the enum after this switch was written.
        ChordCharacter? named = quality switch
        {
            ChordQuality.Major or ChordQuality.Major6 => ChordCharacter.Bright,
            ChordQuality.Add9 or ChordQuality.Add11 or ChordQuality.Major7 => ChordCharacter.Dreamy,
            // A minor sixth chord is the melodic-minor tonic: a coloured resting chord, and the
            // same character as the minor triad it is built on. Reading it as the half-diminished
            // seventh it shares its pitch classes with reported it as Dark, with the stability of
            // an unresolved chord, on a perfect fifth it plainly has.
            ChordQuality.Minor or ChordQuality.MinorMajor7 or ChordQuality.Minor6
                => ChordCharacter.Melancholic,
            ChordQuality.Minor7 => ChordCharacter.Warm,
            ChordQuality.Dominant7 => ChordCharacter.Tense,
            ChordQuality.Diminished or ChordQuality.Diminished7 or ChordQuality.HalfDim7 => ChordCharacter.Dark,
            // ChordCharacter.Mysterious is documented as "augmented, altered dominants".
            ChordQuality.Augmented or ChordQuality.Augmented7
                or ChordQuality.Dominant7Flat5 => ChordCharacter.Mysterious,
            ChordQuality.Sus2 or ChordQuality.Sus4 => ChordCharacter.Suspended,
            ChordQuality.Power => ChordCharacter.Powerful,
            ChordQuality.Quartal => ChordCharacter.Modal,
            _ => null
        };

        if (named is not { } character)
            return ChordCharacterClassification.Unknown;

        // Simple, intuitive scales: 0..1.
        var stability = character switch
        {
            ChordCharacter.Stable => 0.90f,
            ChordCharacter.Bright => 0.80f,
            ChordCharacter.Warm => 0.70f,
            ChordCharacter.Dreamy => 0.60f,
            ChordCharacter.Melancholic => 0.60f,
            ChordCharacter.Powerful => 0.75f,
            ChordCharacter.Modal => 0.55f,
            ChordCharacter.Suspended => 0.45f,
            ChordCharacter.Mysterious => 0.40f,
            ChordCharacter.Dark => 0.25f,
            ChordCharacter.Tense => 0.30f,
            ChordCharacter.Heroic => 0.50f,
            _ => 0.50f
        };

        var brightness = character switch
        {
            ChordCharacter.Bright => 0.85f,
            ChordCharacter.Dreamy => 0.70f,
            ChordCharacter.Stable => 0.65f,
            ChordCharacter.Powerful => 0.60f,
            ChordCharacter.Suspended => 0.55f,
            ChordCharacter.Warm => 0.45f,
            ChordCharacter.Modal => 0.50f,
            ChordCharacter.Mysterious => 0.55f,
            ChordCharacter.Melancholic => 0.30f,
            ChordCharacter.Tense => 0.45f,
            ChordCharacter.Dark => 0.20f,
            ChordCharacter.Heroic => 0.70f,
            _ => 0.50f
        };

        return new ChordCharacterClassification(
            mood: character.ToString(),
            stability: stability,
            brightness: brightness,
            character: character,
            quality: quality);
    }
}

/// <summary>
/// Example-friendly chord character descriptor.
/// </summary>
public sealed record ChordCharacterClassification
{
    // Produced by ChordCharacterClassifier; not constructible by consumers (#18 API freeze).
    internal ChordCharacterClassification(
        string mood,
        float stability,
        float brightness,
        ChordCharacter character,
        ChordQuality quality)
    {
        Mood = mood;
        Stability = stability;
        Brightness = brightness;
        Character = character;
        Quality = quality;
    }

    /// <summary>Character name as text (e.g. <c>Bright</c>).</summary>
    public string Mood { get; init; }

    /// <summary>Perceived stability, 0..1.</summary>
    public float Stability { get; init; }

    /// <summary>Perceived brightness, 0..1.</summary>
    public float Brightness { get; init; }

    /// <summary>Coarse emotional character.</summary>
    public ChordCharacter Character { get; init; }

    /// <summary>The underlying chord quality.</summary>
    public ChordQuality Quality { get; init; }

    /// <summary>
    /// Fallback classification for blank or unparsable chord symbols, and for a chord whose
    /// quality this table does not name. Its character is <see cref="ChordCharacter.Modal"/> —
    /// "non-functional harmony", the same reading <see cref="ProgressionAdvisor"/> gives a
    /// sonority it cannot name — with both scales at the midpoint.
    /// </summary>
    /// <remarks>
    /// It carried <see cref="ChordCharacter.Stable"/>, "tonic, at rest", the highest stability
    /// there is, beside the mood "Unknown": one answer saying the chord is unknown and the
    /// other that it is home. A 7sus4, which no template names, was a resting chord here and a
    /// non-functional one in the advisor's report of the same progression.
    /// </remarks>
    public static ChordCharacterClassification Unknown { get; } = new(
        "Unknown",
        0.5f,
        0.5f,
        ChordCharacter.Modal,
        ChordQuality.Unknown);
}

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
            var pitches = ProgressionAdvisor.ParseChordSymbol(chordSymbol.Trim());
            if (pitches.Length == 0)
            {
                // ParseChordSymbol yields an empty array for anything it cannot parse.
                // Falling through would build a zero mask, and ChordLibrary.GetChord(0)
                // lands on the quality switch's default arm — reporting unparsable input
                // as a maximally stable chord instead of Unknown.
                return ChordCharacterClassification.Unknown;
            }

            var mask = ChordAnalyzer.GetMask(pitches);
            var info = ChordLibrary.GetChord(mask);
            return FromQuality(info.Quality);
        }
        catch
        {
            return ChordCharacterClassification.Unknown;
        }
    }

    private static ChordCharacterClassification FromQuality(ChordQuality quality)
    {
        var character = quality switch
        {
            ChordQuality.Major => ChordCharacter.Bright,
            ChordQuality.Add9 or ChordQuality.Add11 or ChordQuality.Major7 => ChordCharacter.Dreamy,
            ChordQuality.Minor or ChordQuality.MinorMajor7 => ChordCharacter.Melancholic,
            ChordQuality.Minor7 => ChordCharacter.Warm,
            ChordQuality.Dominant7 => ChordCharacter.Tense,
            ChordQuality.Diminished or ChordQuality.Diminished7 or ChordQuality.HalfDim7 => ChordCharacter.Dark,
            ChordQuality.Augmented or ChordQuality.Augmented7 => ChordCharacter.Mysterious,
            ChordQuality.Sus2 or ChordQuality.Sus4 => ChordCharacter.Suspended,
            ChordQuality.Power => ChordCharacter.Powerful,
            ChordQuality.Quartal => ChordCharacter.Modal,
            _ => ChordCharacter.Stable
        };

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

    /// <summary>Fallback classification for blank or unparsable chord symbols.</summary>
    public static ChordCharacterClassification Unknown { get; } = new(
        "Unknown",
        0.5f,
        0.5f,
        ChordCharacter.Stable,
        ChordQuality.Unknown);
}

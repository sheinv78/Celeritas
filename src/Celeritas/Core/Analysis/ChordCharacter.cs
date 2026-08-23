// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Emotional/sonic character of a chord.
/// </summary>
public enum ChordCharacter
{
    /// <summary>Tonic, at rest ("home").</summary>
    Stable,         // Tonic, home

    /// <summary>Major with extensions.</summary>
    Warm,           // Major with extensions

    /// <summary>Maj7 / add9 colors.</summary>
    Dreamy,         // Maj7, add9

    /// <summary>Minor.</summary>
    Melancholic,    // Minor

    /// <summary>Dominant or diminished.</summary>
    Tense,          // Dominant, diminished

    /// <summary>Major dominant in a minor key.</summary>
    Heroic,         // Major dominant in minor key

    /// <summary>Minor with flat 5, or diminished.</summary>
    Dark,           // Minor with b5, diminished

    /// <summary>Sus chords; unresolved.</summary>
    Suspended,      // Sus chords - unresolved

    /// <summary>Major triads.</summary>
    Bright,         // Major triads

    /// <summary>Augmented or altered dominants.</summary>
    Mysterious,     // Augmented, altered dominants

    /// <summary>Power chords.</summary>
    Powerful,       // Power chords

    /// <summary>Quartal or non-functional harmony.</summary>
    Modal           // Quartal, non-functional
}

/// <summary>
/// Detailed analysis of a single chord in context.
/// </summary>
public sealed class ChordAnalysisDetail
{
    // Produced by analysis; not constructible by consumers (#18 API freeze).
    internal ChordAnalysisDetail() { }

    /// <summary>Chord symbol (e.g., "Gm", "D#maj7")</summary>
    public required string Symbol { get; init; }

    /// <summary>Notes in the chord</summary>
    public required string[] Notes { get; init; }

    /// <summary>Roman numeral in key (e.g., "i", "VI", "V"), or "?" if non-diatonic.</summary>
    public required string RomanNumeral { get; init; }

    /// <summary>Nashville Number System label in key (e.g., "6m", "1", "5"), or "?" if non-diatonic.</summary>
    public required string Nashville { get; init; }

    /// <summary>Functional role</summary>
    public required string Function { get; init; }

    /// <summary>Emotional character</summary>
    public required ChordCharacter Character { get; init; }

    /// <summary>Human-readable description of the chord's effect</summary>
    public required string Description { get; init; }

    /// <summary>Special features (e.g., "adds dreamy quality via major 7th")</summary>
    public string? SpecialNote { get; init; }

    /// <summary>Is this chord borrowed from parallel mode?</summary>
    public bool IsBorrowed { get; init; }

    /// <summary>Does this chord use raised/lowered scale degrees?</summary>
    public bool UsesAlteredScale { get; init; }

    /// <summary>Which altered notes are present (e.g., "F# instead of F")</summary>
    public string? AlteredNotes { get; init; }
}

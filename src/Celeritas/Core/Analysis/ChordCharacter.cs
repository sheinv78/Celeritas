// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Emotional/sonic character of a chord.
/// </summary>
public enum ChordCharacter
{
    Stable,         // Tonic, home
    Warm,           // Major with extensions
    Dreamy,         // Maj7, add9
    Melancholic,    // Minor
    Tense,          // Dominant, diminished
    Heroic,         // Major dominant in minor key
    Dark,           // Minor with b5, diminished
    Suspended,      // Sus chords - unresolved
    Bright,         // Major triads
    Mysterious,     // Augmented, altered dominants
    Powerful,       // Power chords
    Modal           // Quartal, non-functional
}

/// <summary>
/// Detailed analysis of a single chord in context.
/// </summary>
public sealed class ChordAnalysisDetail
{
    /// <summary>Chord symbol (e.g., "Gm", "D#maj7")</summary>
    public required string Symbol { get; init; }

    /// <summary>Notes in the chord</summary>
    public required string[] Notes { get; init; }

    /// <summary>Roman numeral in key (e.g., "i", "VI", "V")</summary>
    public required string RomanNumeral { get; init; }

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

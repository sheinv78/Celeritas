// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Result of melody harmonization.
/// </summary>
public sealed class HarmonizationResult
{
    // Produced by the harmonizer; not constructible by consumers (#18 API freeze).
    internal HarmonizationResult() { }

    /// <summary>
    /// The detected or specified key.
    /// </summary>
    public KeySignature Key { get; init; }

    /// <summary>
    /// Chord assignments for each time slice.
    /// </summary>
    public IReadOnlyList<ChordAssignment> Chords { get; init; } = [];

    /// <summary>
    /// Total cost of this harmonization (lower = better).
    /// </summary>
    public float TotalCost { get; init; }

    /// <summary>
    /// The chord symbols of <see cref="Chords"/> in order — "C", "Dm", "Bdim" — spelled with flats
    /// when <see cref="Key"/> is written with flats (F, Bb, Eb, Ab and Db major and their relative
    /// minors D, G, C, F and Bb; pitch class 6 is F# major and pitch class 3 minor is D# minor, as
    /// <see cref="KeySignature.ToString"/> names them), so a harmonization can be handed straight to
    /// <see cref="Analysis.ProgressionAdvisor"/> and the other readers of symbols.
    /// </summary>
    /// <remarks>
    /// These used to be the display names <see cref="ChordInfo.ToString"/> gives, "B Diminished"
    /// and "A# Major", of which <see cref="Analysis.ProgressionAdvisor.TryParseChordSymbol(string, out int[])"/>
    /// happens to read only the major and minor ones: every diminished chord was refused, and a
    /// flat key came back spelled in sharps. A chord whose quality has no conventional symbol still
    /// reads as its name, e.g. "C Unknown".
    /// </remarks>
    public IEnumerable<string> GetSymbols() => Chords.Select(c => c.Chord.ToSymbol(preferSharps: !KeySpelling.UsesFlats(Key)));
}

/// <summary>
/// A chord assigned to a specific time slice.
/// </summary>
public readonly record struct ChordAssignment(
    Rational Start,
    Rational End,
    ChordInfo Chord,
    int[] Pitches);

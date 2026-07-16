// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Type of modulation or tonicization.
/// </summary>
public enum ModulationType
{
    /// <summary>Brief emphasis on a non-tonic chord (returns quickly)</summary>
    Tonicization,

    /// <summary>Pivot chord modulation (chord belongs to both keys)</summary>
    PivotChord,

    /// <summary>Direct/phrase modulation (abrupt key change)</summary>
    Direct,

    /// <summary>Sequential modulation (pattern repeated in new key)</summary>
    Sequential,

    /// <summary>Chromatic modulation (chromatic alteration leads to new key)</summary>
    Chromatic,

    /// <summary>Enharmonic modulation (reinterpretation of chord)</summary>
    Enharmonic,

    /// <summary>Modal interchange (parallel major/minor)</summary>
    ModalInterchange
}

/// <summary>
/// Information about a detected modulation or tonicization.
/// </summary>
public sealed class ModulationInfo
{
    // Produced by analysis; not constructible by consumers (#18 API freeze).
    internal ModulationInfo() { }

    /// <summary>Position in the progression (0-based)</summary>
    public required int Position { get; init; }

    /// <summary>Key before the modulation</summary>
    public required KeySignature FromKey { get; init; }

    /// <summary>Key after the modulation</summary>
    public required KeySignature ToKey { get; init; }

    /// <summary>Type of modulation</summary>
    public required ModulationType Type { get; init; }

    /// <summary>Pivot chord if applicable</summary>
    public string? PivotChord { get; init; }

    /// <summary>Analysis of the pivot chord in both keys</summary>
    public string? PivotAnalysis { get; init; }

    /// <summary>Human-readable description</summary>
    public required string Description { get; init; }

    /// <summary>How many chords does this key area last?</summary>
    public int Duration { get; init; } = 1;

    /// <summary>Relationship between keys (e.g., "relative major", "dominant key")</summary>
    public required string KeyRelationship { get; init; }

    public override string ToString() =>
        $"{Type}: {FromKey} → {ToKey} at position {Position + 1}";
}

/// <summary>
/// Helper methods for key relationships.
/// </summary>
public static class KeyRelationships
{
    /// <summary>
    /// Describe the relationship between two keys.
    /// </summary>
    public static string Describe(KeySignature from, KeySignature to)
    {
        var interval = (((to.Root - from.Root) % 12) + 12) % 12;

        // Mode-aware arms (relative keys, diatonic mediants) must precede the generic
        // chromatic-mediant arms so they remain reachable.
        return interval switch
        {
            // Same root
            0 when from.IsMajor != to.IsMajor => to.IsMajor ? "parallel major" : "parallel minor",
            0 => "same key",
            // Relative major/minor (opposite modes)
            3 when !from.IsMajor && to.IsMajor => "relative major",
            9 when from.IsMajor && !to.IsMajor => "relative minor",
            // Diatonic mediant/submediant (opposite modes)
            4 when from.IsMajor && !to.IsMajor => "mediant key (iii)",
            8 when !from.IsMajor && to.IsMajor => "submediant key (VI)",
            // Dominant key (5th above)
            7 => "dominant key (V)",
            // Subdominant key (4th above / 5th below)
            5 => "subdominant key (IV)",
            // Supertonic
            2 => "supertonic key (II)",
            // Third relationships (romantic / chromatic mediants)
            3 => "chromatic mediant (up m3)",
            4 => "chromatic mediant (up M3)",
            8 => "chromatic mediant (down M3)",
            9 => "chromatic mediant (down m3)",
            // Chromatic relationships
            1 => "chromatic: up half step",
            11 => "chromatic: down half step",
            6 => "tritone key",
            _ => $"distant key ({interval} semitones)"
        };
    }

    /// <summary>
    /// Check if two keys are closely related (differ by at most one accidental).
    /// Closely related keys are: the key itself, its relative, the dominant and
    /// subdominant keys, and their relatives. For C major: G, F, Am, Em, Dm.
    /// Parallel keys (C major / C minor) differ by three accidentals and are NOT
    /// closely related under the standard definition.
    /// </summary>
    public static bool AreCloselyRelated(KeySignature a, KeySignature b)
    {
        // Normalize each key to its relative-major root, then compare positions
        // on the circle of fifths: closely related = same position or one step away.
        var relA = a.IsMajor ? a.Root : (a.Root + 3) % 12;
        var relB = b.IsMajor ? b.Root : (b.Root + 3) % 12;
        var interval = (((relB - relA) % 12) + 12) % 12;

        return interval is 0 or 5 or 7;
    }

    /// <summary>
    /// Get the number of common tones between two keys (0-7).
    /// </summary>
    public static int CommonTones(KeySignature a, KeySignature b)
    {
        var maskA = GetScaleMask(a);
        var maskB = GetScaleMask(b);
        return BitOperations.PopCount((uint)(maskA & maskB));
    }

    private static ushort GetScaleMask(KeySignature key)
    {
        // Major scale intervals: 0, 2, 4, 5, 7, 9, 11
        // Minor scale intervals: 0, 2, 3, 5, 7, 8, 10
        int[] intervals = key.IsMajor
            ? [0, 2, 4, 5, 7, 9, 11]
            : [0, 2, 3, 5, 7, 8, 10];

        ushort mask = 0;
        foreach (var i in intervals)
        {
            mask |= (ushort)(1 << ((key.Root + i) % 12));
        }
        return mask;
    }
}

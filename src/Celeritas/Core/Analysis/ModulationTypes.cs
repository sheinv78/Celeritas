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

        return interval switch
        {
            // Same root, different mode
            0 when from.IsMajor != to.IsMajor => to.IsMajor ? "parallel major" : "parallel minor",
            // Relative major/minor (3 semitones apart, opposite modes)
            3 when !from.IsMajor && to.IsMajor => "relative major",
            9 when from.IsMajor && !to.IsMajor => "relative minor",
            // Dominant key (5th above)
            7 => "dominant key (V)",
            // Subdominant key (4th above / 5th below)
            5 => "subdominant key (IV)",
            // Secondary dominants
            2 => "supertonic key (II)",
            4 => "mediant key (III)",
            9 => "submediant key (VI)",
            _ => interval switch
            {
                // Chromatic relationships
                1 => "chromatic: up half step",
                11 => "chromatic: down half step",
                6 => "tritone key",
                // Third relationships (romantic)
                3 => "chromatic mediant (down m3)",
                4 => "chromatic mediant (up M3)",
                8 => "chromatic mediant (down M3)",
                9 => "chromatic mediant (up m3)",
                _ => $"distant key ({interval} semitones)"
            }
        };
    }

    /// <summary>
    /// Check if two keys are closely related (share most notes).
    /// </summary>
    public static bool AreCloselyRelated(KeySignature a, KeySignature b)
    {
        var interval = (((b.Root - a.Root) % 12) + 12) % 12;

        return interval switch
        {
            // Same key
            0 => true,
            _ => interval switch
            {
                // Parallel major/minor
                0 when a.IsMajor != b.IsMajor => true,
                // Relative major/minor
                3 or 9 when a.IsMajor != b.IsMajor => true,
                // Dominant/Subdominant
                5 or 7 => true,
                _ => false
            }
        };
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

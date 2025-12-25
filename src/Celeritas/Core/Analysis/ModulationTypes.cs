// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

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
        var interval = ((to.Root - from.Root) % 12 + 12) % 12;

        // Same root, different mode
        if (interval == 0 && from.IsMajor != to.IsMajor)
        {
            return to.IsMajor ? "parallel major" : "parallel minor";
        }

        // Relative major/minor (3 semitones apart, opposite modes)
        if (interval == 3 && !from.IsMajor && to.IsMajor)
            return "relative major";
        if (interval == 9 && from.IsMajor && !to.IsMajor)
            return "relative minor";

        // Dominant key (5th above)
        if (interval == 7)
            return "dominant key (V)";

        // Subdominant key (4th above / 5th below)
        if (interval == 5)
            return "subdominant key (IV)";

        // Secondary dominants
        if (interval == 2) return "supertonic key (II)";
        if (interval == 4) return "mediant key (III)";
        if (interval == 9) return "submediant key (VI)";

        // Chromatic relationships
        if (interval == 1) return "chromatic: up half step";
        if (interval == 11) return "chromatic: down half step";
        if (interval == 6) return "tritone key";

        // Third relationships (romantic)
        if (interval == 3) return "chromatic mediant (down m3)";
        if (interval == 4) return "chromatic mediant (up M3)";
        if (interval == 8) return "chromatic mediant (down M3)";
        if (interval == 9) return "chromatic mediant (up m3)";

        return $"distant key ({interval} semitones)";
    }

    /// <summary>
    /// Check if two keys are closely related (share most notes).
    /// </summary>
    public static bool AreCloselyRelated(KeySignature a, KeySignature b)
    {
        var interval = ((b.Root - a.Root) % 12 + 12) % 12;

        // Same key
        if (interval == 0) return true;

        // Parallel major/minor
        if (interval == 0 && a.IsMajor != b.IsMajor) return true;

        // Relative major/minor
        if ((interval == 3 || interval == 9) && a.IsMajor != b.IsMajor) return true;

        // Dominant/Subdominant
        if (interval == 5 || interval == 7) return true;

        return false;
    }

    /// <summary>
    /// Get the number of common tones between two keys (0-7).
    /// </summary>
    public static int CommonTones(KeySignature a, KeySignature b)
    {
        var maskA = GetScaleMask(a);
        var maskB = GetScaleMask(b);
        return System.Numerics.BitOperations.PopCount((uint)(maskA & maskB));
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

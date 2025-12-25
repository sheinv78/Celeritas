// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Complete analysis report for a chord progression.
/// </summary>
public sealed class ProgressionReport
{
    /// <summary>Detected key signature</summary>
    public required KeySignature Key { get; init; }

    /// <summary>Confidence in key detection (0-1)</summary>
    public required float KeyConfidence { get; init; }

    /// <summary>Detailed analysis of each chord</summary>
    public required IReadOnlyList<ChordAnalysisDetail> Chords { get; init; }

    /// <summary>Detected cadences</summary>
    public required IReadOnlyList<CadenceInfo> Cadences { get; init; }

    /// <summary>Detected modulations and tonicizations</summary>
    public required IReadOnlyList<ModulationInfo> Modulations { get; init; }

    /// <summary>Overall progression pattern (e.g., "i - VI - iv - i - V")</summary>
    public required string Pattern { get; init; }

    /// <summary>Does progression use harmonic minor (raised 7th)?</summary>
    public bool UsesHarmonicMinor { get; init; }

    /// <summary>Does progression use melodic minor?</summary>
    public bool UsesMelodicMinor { get; init; }

    /// <summary>Does progression have modal mixture/borrowed chords?</summary>
    public bool HasModalMixture { get; init; }

    /// <summary>Suggestions for improvement or resolution</summary>
    public required IReadOnlyList<string> Suggestions { get; init; }

    /// <summary>Overall narrative/story of the progression</summary>
    public required string Narrative { get; init; }

    /// <summary>Generate a formatted text report</summary>
    public string ToFormattedReport()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"=== Progression Analysis ===");
        sb.AppendLine($"Key: {Key} (confidence: {KeyConfidence:P0})");
        sb.AppendLine($"Pattern: {Pattern}");
        sb.AppendLine();

        sb.AppendLine("--- Chord Breakdown ---");
        for (var i = 0; i < Chords.Count; i++)
        {
            var c = Chords[i];
            sb.AppendLine($"{i + 1}. {c.Symbol} ({c.RomanNumeral}) - {c.Function}");
            sb.AppendLine($"   Notes: {string.Join(", ", c.Notes)}");
            sb.AppendLine($"   Character: {c.Character} - {c.Description}");
            if (c.SpecialNote != null)
                sb.AppendLine($"   Note: {c.SpecialNote}");
            if (c.UsesAlteredScale)
                sb.AppendLine($"   Altered: {c.AlteredNotes}");
            sb.AppendLine();
        }

        if (Cadences.Count > 0)
        {
            sb.AppendLine("--- Cadences ---");
            foreach (var cad in Cadences)
            {
                sb.AppendLine($"At position {cad.Position + 1}: {cad.Type} ({cad.FromChord} -> {cad.ToChord})");
                sb.AppendLine($"   {cad.Description}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("--- Narrative ---");
        sb.AppendLine(Narrative);
        sb.AppendLine();

        if (Suggestions.Count > 0)
        {
            sb.AppendLine("--- Suggestions ---");
            foreach (var sug in Suggestions)
            {
                sb.AppendLine($"* {sug}");
            }
        }

        return sb.ToString();
    }
}

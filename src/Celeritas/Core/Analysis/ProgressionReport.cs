// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// A suggested chord that could follow a progression.
/// </summary>
public sealed class ChordSuggestion
{
    /// <summary>Chord symbol (e.g., "Cmaj7", "Dm")</summary>
    public string Chord { get; init; }

    /// <summary>Reason why this chord is suggested</summary>
    public string Reason { get; init; }

    /// <summary>Quality score (0-1) indicating how well it fits</summary>
    public float Score { get; init; }

    public ChordSuggestion(string chord, string reason, float score)
    {
        Chord = chord;
        Reason = reason;
        Score = score;
    }
}

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

    /// <summary>Short human-readable summary.</summary>
    public string Summary { get; init; } = "";

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

    /// <summary>Complexity level of the progression (0-1, higher = more complex)</summary>
    public float Complexity { get; init; }

    /// <summary>Average harmonic tension across the progression (0-1)</summary>
    public float AverageTension { get; init; }

    /// <summary>Array of tension values for each chord (0-1, used for tension curves)</summary>
    public float[]? TensionCurve { get; init; }

    /// <summary>Notable highlights or interesting moments in the progression</summary>
    public IReadOnlyList<string> Highlights { get; init; } = [];

    /// <summary>Detected secondary dominants (tonicizations).</summary>
    public IReadOnlyList<SecondaryDominantInfo> SecondaryDominants { get; init; } = [];

    /// <summary>Quick flag: any secondary dominants?</summary>
    public bool HasSecondaryDominants => SecondaryDominants.Count > 0;

    /// <summary>Detected borrowed chords (modal mixture).</summary>
    public IReadOnlyList<BorrowedChordInfo> BorrowedChords { get; init; } = [];

    /// <summary>Quick flag: any borrowed chords?</summary>
    public bool HasBorrowedChords => BorrowedChords.Count > 0;

    /// <summary>Voice-leading smoothness (0-1, higher = smoother).</summary>
    public float Smoothness { get; init; }

    /// <summary>Average voice movement between chords in semitones.</summary>
    public float AverageMovement { get; init; }

    /// <summary>Approximate count of parallel fifths detected between adjacent chords.</summary>
    public int ParallelFifths { get; init; }

    /// <summary>Approximate count of parallel octaves/unisons detected between adjacent chords.</summary>
    public int ParallelOctaves { get; init; }

    /// <summary>Overall voice-leading quality rating (free-form).</summary>
    public string QualityRating { get; init; } = "";

    /// <summary>
    /// Convenience factory matching the examples.
    /// </summary>
    public static ProgressionReport Generate(string[] chordSymbols) => ProgressionAdvisor.Analyze(chordSymbols);

    /// <summary>Generate a formatted text report</summary>
    public string ToFormattedReport()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"=== Progression Analysis ===");
        sb.AppendLine($"Key: {Key} (confidence: {KeyConfidence:P0})");
        sb.AppendLine($"Pattern: {Pattern}");

        if (!string.IsNullOrWhiteSpace(Summary))
            sb.AppendLine($"Summary: {Summary}");

        if (TensionCurve is { Length: > 0 })
            sb.AppendLine($"Avg tension: {AverageTension:P0} (complexity: {Complexity:P0})");

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

        if (Highlights.Count > 0)
        {
            sb.AppendLine("--- Highlights ---");
            foreach (var h in Highlights)
                sb.AppendLine($"* {h}");
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

/// <summary>
/// A detected secondary dominant / tonicization event.
/// </summary>
public sealed class SecondaryDominantInfo
{
    public required string Chord { get; init; }
    public required string Target { get; init; }
    public string? TargetDegree { get; init; }
    public int Position { get; init; }
}

/// <summary>
/// A detected borrowed chord (modal mixture).
/// </summary>
public sealed class BorrowedChordInfo
{
    public required string Chord { get; init; }
    public required string SourceKey { get; init; }
    public int Position { get; init; }
}

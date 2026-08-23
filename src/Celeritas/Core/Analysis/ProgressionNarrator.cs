// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Text;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Prose generation for progression reports: narrative text, improvement
/// suggestions, and the human-readable character/function display strings.
/// Split out of <see cref="ProgressionAdvisor"/> so that detection logic and
/// text generation live in separate concerns. Output strings are identical to
/// the previous inline implementation.
/// </summary>
internal static class ProgressionNarrator
{
    public static string GetFunctionName(HarmonicFunction function) => function switch
    {
        HarmonicFunction.Tonic => "Tonic (home/stable)",
        HarmonicFunction.Subdominant => "Subdominant (motion/tension building)",
        HarmonicFunction.Dominant => "Dominant (tension/pull to resolve)",
        HarmonicFunction.Chromatic => "Chromatic (color/borrowed)",
        _ => "Unknown"
    };

    public static string GetCharacterDescription(ChordCharacter character, int position, int total)
    {
        var positionDesc = position == 0 ? "Opening" : position == total - 1 ? "Closing" : "Continuing";

        var charDesc = character switch
        {
            ChordCharacter.Stable => "stable and grounded, feels like home",
            ChordCharacter.Warm => "warm and expressive",
            ChordCharacter.Dreamy => "dreamy, floating, sophisticated",
            ChordCharacter.Melancholic => "melancholic, introspective",
            ChordCharacter.Tense => "tense, demanding resolution",
            ChordCharacter.Heroic => "heroic, powerful, dramatic",
            ChordCharacter.Dark => "dark, unstable, anxious",
            ChordCharacter.Suspended => "suspended, unresolved, anticipating",
            ChordCharacter.Bright => "bright and optimistic",
            ChordCharacter.Mysterious => "mysterious, otherworldly",
            ChordCharacter.Powerful => "powerful, open, ambiguous",
            ChordCharacter.Modal => "modal, modern, non-functional",
            _ => "neutral"
        };

        return $"{positionDesc}: {charDesc}";
    }

    public static string GenerateNarrative(
        List<ChordAnalysisDetail> chords,
        List<CadenceInfo> cadences,
        KeySignature key,
        bool usesHarmonicMinor,
        List<ModulationInfo> modulations,
        RomanNumeralChord[] romans)
    {
        var sb = new StringBuilder();

        var keyDesc = key.IsMajor ? "bright and optimistic" : "darker and more dramatic";
        sb.AppendLine($"This progression is in {key}, giving it a {keyDesc} character.");

        if (usesHarmonicMinor)
        {
            sb.AppendLine("The use of raised 7th (harmonic minor) creates a strong pull toward resolution, adding drama and intensity.");
        }

        // Describe modulations
        if (modulations.Count > 0)
        {
            foreach (var mod in modulations)
            {
                if (mod.Type == ModulationType.Tonicization)
                {
                    sb.AppendLine($"At chord {mod.Position + 1}: brief tonicization to {mod.ToKey} ({mod.KeyRelationship}) - creates momentary tonal shift.");
                }
                else
                {
                    sb.AppendLine($"At chord {mod.Position + 1}: modulation to {mod.ToKey} ({mod.KeyRelationship}) via {mod.Type.ToString().ToLowerInvariant()}.");
                }
            }
        }

        // Describe the journey
        sb.Append("The harmonic journey: ");
        var phases = new List<string>();

        foreach (var chord in chords)
        {
            var desc = chord.Function switch
            {
                "Tonic (home/stable)" => "establishes home",
                "Subdominant (motion/tension building)" => "builds tension",
                "Dominant (tension/pull to resolve)" => "creates strong pull to resolve",
                _ => "adds color"
            };
            phases.Add(desc);
        }

        sb.AppendLine(string.Join(" → ", phases) + ".");

        // Comment on ending. A cadence only describes the ENDING when its position
        // is the final chord pair — the last detected cadence may sit mid-progression.
        var endingCadence = cadences.Count > 0 && cadences[^1].Position == chords.Count - 2
            ? cadences[^1].Type
            : CadenceType.None;

        if (endingCadence != CadenceType.None)
        {
            if (endingCadence == CadenceType.Deceptive)
            {
                sb.AppendLine("Note: The ending uses a deceptive cadence - instead of resolving home, it takes an unexpected turn. This creates a 'to be continued' feeling.");
            }
            else if (endingCadence is CadenceType.Half or CadenceType.Phrygian)
            {
                sb.AppendLine("Note: The progression ends on dominant - this creates unresolved tension, like an open question.");
            }
            else if (endingCadence == CadenceType.Authentic)
            {
                sb.AppendLine("The authentic cadence at the end provides a satisfying, conclusive finish.");
            }
        }
        else if (chords.Count > 0)
        {
            // Ends-on-tonic is a scale-degree question: a minor-key i (Melancholic)
            // resolves home just as a major-key I (Stable) does. The old check
            // required Character == Stable and so called minor endings unresolved.
            var lastRoman = romans[^1];
            if (lastRoman.IsValid && lastRoman.Degree == ScaleDegree.I)
            {
                sb.AppendLine("The progression ends on the tonic, providing a sense of completion.");
            }
            else
            {
                sb.AppendLine("The progression doesn't resolve to tonic at the end, leaving it somewhat open.");
            }
        }

        return sb.ToString().Trim();
    }

    public static List<string> GenerateSuggestions(
        List<ChordAnalysisDetail> chords,
        List<CadenceInfo> cadences,
        KeySignature key,
        List<ParsedChord> parsedChords,
        RomanNumeralChord[] romans,
        List<ModulationInfo> modulations)
    {
        var suggestions = new List<string>();

        if (chords.Count == 0)
        {
            return suggestions;
        }

        var lastRoman = romans[^1];
        var tonicSymbol = key.IsMajor ? ChordLibrary.NoteNames[key.Root] : ChordLibrary.NoteNames[key.Root] + "m";

        // Modulation suggestions
        foreach (var mod in modulations)
        {
            if (mod.Type == ModulationType.Tonicization)
            {
                // Suggest extending or confirming the tonicization
                var secDomRoot = (mod.ToKey.Root + 7) % 12;
                suggestions.Add($"The {mod.ToKey} tonicization at chord {mod.Position + 1} could be extended with {ChordLibrary.NoteNames[secDomRoot]}7 → {ChordLibrary.NoteNames[mod.ToKey.Root]}{(mod.ToKey.IsMajor ? "" : "m")} for stronger effect.");
            }
            else if (!KeyRelationships.AreCloselyRelated(mod.FromKey, mod.ToKey))
            {
                // Suggest pivot chord for distant modulation
                suggestions.Add($"The modulation to {mod.ToKey} is distant. Consider adding a pivot chord that belongs to both {mod.FromKey} and {mod.ToKey} for smoother transition.");
            }
        }

        // Track what we've already suggested to avoid duplicates
        var suggestedResolution = false;

        // A cadence only describes the ENDING when its position is the final chord
        // pair — cadences.LastOrDefault() alone may be a mid-progression cadence,
        // and treating it as the ending contradicted the ending suggestions below.
        var lastCadence = cadences.Count > 0 && cadences[^1].Position == parsedChords.Count - 2
            ? cadences[^1]
            : default;

        // Check for deceptive cadence at end
        if (lastCadence.Type == CadenceType.Deceptive)
        {
            suggestions.Add($"The deceptive cadence (V→vi) creates surprise and openness. For a conclusive finish, resolve to {tonicSymbol}.");
            suggestedResolution = true;
        }

        // Check for half cadence at end (ends on V; Phrygian is a half cadence too)
        if (lastCadence.Type is CadenceType.Half or CadenceType.Phrygian)
        {
            suggestions.Add($"Ending on the dominant (V) creates suspense. Add {tonicSymbol} for complete resolution.");
            suggestedResolution = true;
        }

        // Check for plagal cadence - suggest authentic alternative
        if (lastCadence.Type == CadenceType.Plagal)
        {
            var dominantRoot = (key.Root + 7) % 12;
            var dominantSymbol = ChordLibrary.NoteNames[dominantRoot] + "7";
            suggestions.Add($"The plagal cadence (IV→I) is gentle. For more drama, try {dominantSymbol}→{tonicSymbol} (authentic cadence).");
        }

        // Authentic cadence - positive reinforcement
        if (lastCadence.Type == CadenceType.Authentic && !suggestedResolution)
        {
            suggestions.Add("Strong authentic cadence provides satisfying resolution.");
        }

        // If not ending on tonic and we haven't already suggested resolution.
        // An invalid (chromatic) final chord is NOT the tonic even though
        // Invalid's default Degree is ScaleDegree.I.
        if (!suggestedResolution && (!lastRoman.IsValid || lastRoman.Degree != ScaleDegree.I))
        {
            if (lastRoman.IsValid && lastRoman.Degree == ScaleDegree.V)
            {
                suggestions.Add($"The progression ends on the dominant. Add {tonicSymbol} for full closure.");
            }
            else if (lastRoman.Degree == ScaleDegree.Iv)
            {
                suggestions.Add($"Ending on IV (subdominant) feels unresolved. Try IV→V→{tonicSymbol} for complete cadence.");
            }
            else
            {
                suggestions.Add($"Consider ending on {tonicSymbol} for a sense of completion.");
            }
        }

        // Suggest ii-V-I if ending on I but coming from non-dominant
        if (lastRoman.IsValid && lastRoman.Degree == ScaleDegree.I && chords.Count >= 2)
        {
            var secondLast = romans[^2];
            if (secondLast.Degree != ScaleDegree.V)
            {
                var iiRoot = (key.Root + 2) % 12;
                var vRoot = (key.Root + 7) % 12;
                var iiSymbol = key.IsMajor ? ChordLibrary.NoteNames[iiRoot] + "m7" : ChordLibrary.NoteNames[iiRoot] + "m7b5";
                var vSymbol = ChordLibrary.NoteNames[vRoot] + "7";
                suggestions.Add($"For a jazzier resolution, try {iiSymbol}→{vSymbol}→{tonicSymbol} (ii-V-I turnaround).");
            }
        }

        // Check for harmonic interest
        if (chords.Count >= 4)
        {
            var uniqueChords = parsedChords.Select(c => c.Info.RootPitchClass).Distinct().Count();
            if (uniqueChords <= 2)
            {
                suggestions.Add("The progression uses few unique chords. Consider adding a passing chord for variety.");
            }
        }

        // Suggest modal interchange if all diatonic
        var hasNonDiatonic = chords.Any(c => c.IsBorrowed || c.UsesAlteredScale);
        if (!hasNonDiatonic && chords.Count >= 3)
        {
            if (key.IsMajor)
            {
                var ivMinorRoot = (key.Root + 5) % 12;
                suggestions.Add($"Try {ChordLibrary.NoteNames[ivMinorRoot]}m (borrowed iv) for emotional color.");
            }
            else
            {
                var ivMajorRoot = (key.Root + 5) % 12;
                suggestions.Add($"Try {ChordLibrary.NoteNames[ivMajorRoot]} (borrowed IV) to brighten the mood.");
            }
        }

        return suggestions;
    }
}

// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Represents a detected key change or tonicization.
/// </summary>
public sealed class ModulationEvent
{
    /// <summary>Starting offset of the modulation.</summary>
    public required Rational Offset { get; init; }

    /// <summary>Key before modulation.</summary>
    public required KeySignature FromKey { get; init; }

    /// <summary>Key after modulation.</summary>
    public required KeySignature ToKey { get; init; }

    /// <summary>Type of modulation.</summary>
    public required ModulationType Type { get; init; }

    /// <summary>Confidence in detection (0.0-1.0).</summary>
    public required float Confidence { get; init; }

    /// <summary>Pivot chord if applicable (in both key contexts).</summary>
    public (RomanNumeralChord? FromContext, RomanNumeralChord? ToContext)? PivotChord { get; init; }

    /// <summary>Duration of the new key area (if temporary).</summary>
    public Rational? Duration { get; init; }

    /// <summary>Description of the modulation.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Result of modulation analysis.
/// </summary>
public sealed class ModulationAnalysisResult
{
    /// <summary>Starting key signature.</summary>
    public required KeySignature StartKey { get; init; }

    /// <summary>All detected modulations.</summary>
    public required IReadOnlyList<ModulationEvent> Modulations { get; init; }

    /// <summary>Final key signature.</summary>
    public required KeySignature EndKey { get; init; }

    /// <summary>Number of distinct keys visited.</summary>
    public int KeyCount => Modulations.Select(m => m.ToKey).Append(StartKey).Distinct().Count();

    /// <summary>Number of temporary tonicizations.</summary>
    public int TonicizationCount => Modulations.Count(m => m.Type == ModulationType.Tonicization);

    /// <summary>Number of true modulations (non-temporary).</summary>
    public int TrueModulationCount => Modulations.Count(m => m.Type != ModulationType.Tonicization);
}

/// <summary>
/// Detects key changes, tonicizations, and pivot chords in musical passages.
/// </summary>
public static class ModulationDetector
{
    /// <summary>
    /// Analyze a note buffer for modulations starting from a known key.
    /// </summary>
    public static ModulationAnalysisResult Analyze(NoteBuffer buffer, KeySignature startKey)
    {
        var notes = new NoteEvent[buffer.Count];
        for (int i = 0; i < buffer.Count; i++)
        {
            notes[i] = buffer.Get(i);
        }
        return Analyze(notes, startKey);
    }

    /// <summary>
    /// Analyze a sequence of note events for modulations.
    /// </summary>
    public static ModulationAnalysisResult Analyze(ReadOnlySpan<NoteEvent> notes, KeySignature startKey)
    {
        if (notes.Length == 0)
        {
            return new ModulationAnalysisResult
            {
                StartKey = startKey,
                Modulations = [],
                EndKey = startKey
            };
        }

        var modulations = new List<ModulationEvent>();
        var currentKey = startKey;
        var windowSize = 8; // Number of chords to analyze at once
        var minModulationDuration = new Rational(2, 1); // At least 2 beats

        // Convert to array for easier manipulation
        var notesArray = notes.ToArray();
        var chords = ExtractChords(notesArray);

        if (chords.Count < 2)
        {
            return new ModulationAnalysisResult
            {
                StartKey = startKey,
                Modulations = [],
                EndKey = startKey
            };
        }

        for (int i = windowSize; i < chords.Count; i++)
        {
            var window = chords.Skip(Math.Max(0, i - windowSize)).Take(windowSize).ToList();
            var currentChord = chords[i];

            // Detect key at this position
            var detectedKey = DetectKeyInWindow(window, currentKey);

            if (detectedKey == null || detectedKey.Equals(currentKey))
            {
                continue;
            }

            // Check if this is a real modulation or just a passing chromaticism
            var futureWindow = chords.Skip(i).Take(windowSize / 2).ToList();
            var stability = MeasureKeyStability(futureWindow, detectedKey.Value);

            if (stability < 0.5f)
            {
                continue; // Not stable enough, probably just passing
            }

            // Determine modulation type
            var modulationType = DetermineModulationType(currentKey, detectedKey.Value, currentChord.Offset);
            var confidence = stability;

            // Check duration to distinguish tonicization from true modulation
            var duration = CalculateKeyDuration(chords, i, detectedKey.Value);
            var isTonicization = duration < minModulationDuration;

            if (isTonicization)
            {
                modulationType = ModulationType.Tonicization;
            }

            // Look for pivot chord
            var pivotChord = FindPivotChord(chords, i, currentKey, detectedKey.Value);

            var modulation = new ModulationEvent
            {
                Offset = currentChord.Offset,
                FromKey = currentKey,
                ToKey = detectedKey.Value,
                Type = modulationType,
                Confidence = confidence,
                PivotChord = pivotChord,
                Duration = isTonicization ? duration : null,
                Description = DescribeModulation(currentKey, detectedKey.Value, modulationType, pivotChord)
            };

            modulations.Add(modulation);

            // Update current key if this is a true modulation
            if (!isTonicization)
            {
                currentKey = detectedKey.Value;
            }
        }

        return new ModulationAnalysisResult
        {
            StartKey = startKey,
            Modulations = modulations,
            EndKey = currentKey
        };
    }

    private record ChordEvent(Rational Offset, ushort Mask, int[] PitchClasses);

    private static List<ChordEvent> ExtractChords(NoteEvent[] notes)
    {
        if (notes.Length == 0)
        {
            return [];
        }

        var chords = new List<ChordEvent>();
        var quantizationGrid = new Rational(1, 8); // Eighth note grid

        // Group notes by quantized onset time
        var groups = new Dictionary<Rational, List<int>>();

        foreach (var note in notes)
        {
            var quantizedOffset = QuantizeOffset(note.Offset, quantizationGrid);

            if (!groups.ContainsKey(quantizedOffset))
            {
                groups[quantizedOffset] = [];
            }

            groups[quantizedOffset].Add(note.Pitch);
        }

        // Create chord events from groups with 2+ notes
        foreach (var (offset, pitches) in groups.OrderBy(kvp => kvp.Key))
        {
            if (pitches.Count < 2)
            {
                continue;
            }

            var mask = ChordAnalyzer.GetMask(pitches.ToArray());
            var pitchClasses = PitchClassSetAnalyzer.MaskToPitchClasses(mask);

            chords.Add(new ChordEvent(offset, mask, pitchClasses));
        }

        return chords;
    }

    private static Rational QuantizeOffset(Rational offset, Rational grid)
    {
        var ratio = offset / grid;
        var rounded = (int)Math.Round(ratio.ToDouble());
        return grid * rounded;
    }

    private static KeySignature? DetectKeyInWindow(List<ChordEvent> window, KeySignature currentKey)
    {
        if (window.Count == 0)
        {
            return null;
        }

        // Collect all pitch classes in the window
        var pitchClassCounts = new int[12];

        foreach (var chord in window)
        {
            foreach (var pc in chord.PitchClasses)
            {
                pitchClassCounts[pc]++;
            }
        }

        // Use key profiler to detect key
        var allPitches = new List<int>();
        foreach (var chord in window)
        {
            allPitches.AddRange(chord.PitchClasses);
        }

        if (allPitches.Count == 0)
        {
            return null;
        }

        var detectedKey = KeyAnalyzer.IdentifyKey(allPitches.ToArray());

        // Require significant difference from current key
        if (detectedKey.Root == currentKey.Root && detectedKey.IsMajor == currentKey.IsMajor)
        {
            return null;
        }

        return detectedKey;
    }

    private static float MeasureKeyStability(List<ChordEvent> window, KeySignature key)
    {
        if (window.Count == 0)
        {
            return 0f;
        }

        var scale = key.GetScale();
        var inKeyCount = 0;
        var totalCount = 0;

        foreach (var chord in window)
        {
            foreach (var pc in chord.PitchClasses)
            {
                totalCount++;
                if (scale.Contains(pc))
                {
                    inKeyCount++;
                }
            }
        }

        return totalCount > 0 ? (float)inKeyCount / totalCount : 0f;
    }

    private static ModulationType DetermineModulationType(KeySignature fromKey, KeySignature toKey, Rational offset)
    {
        var interval = (toKey.Root - fromKey.Root + 12) % 12;

        // Parallel key (same tonic)
        if (fromKey.Root == toKey.Root && fromKey.IsMajor != toKey.IsMajor)
        {
            return ModulationType.ModalInterchange;
        }

        // Relative key (minor third apart, opposite modes)
        if ((interval == 3 || interval == 9) && fromKey.IsMajor != toKey.IsMajor)
        {
            return ModulationType.Direct; // Use Direct for relative key changes
        }

        // Chromatic mediant (major or minor third, same mode)
        if ((interval == 3 || interval == 4 || interval == 8 || interval == 9) && fromKey.IsMajor == toKey.IsMajor)
        {
            return ModulationType.Chromatic;
        }

        // Default to direct or pivot chord (requires analysis of actual chords)
        return ModulationType.Direct;
    }

    private static Rational CalculateKeyDuration(List<ChordEvent> chords, int startIndex, KeySignature key)
    {
        var scale = key.GetScale();
        var startOffset = chords[startIndex].Offset;
        var endOffset = startOffset;

        for (int i = startIndex; i < chords.Count; i++)
        {
            var chord = chords[i];
            var inKeyCount = chord.PitchClasses.Count(pc => scale.Contains(pc));
            var outOfKeyCount = chord.PitchClasses.Length - inKeyCount;

            // If more notes are out of key, we've left this key area
            if (outOfKeyCount > inKeyCount)
            {
                break;
            }

            endOffset = chord.Offset;
        }

        return endOffset - startOffset;
    }

    private static (RomanNumeralChord?, RomanNumeralChord?)? FindPivotChord(
        List<ChordEvent> chords,
        int modulationIndex,
        KeySignature fromKey,
        KeySignature toKey)
    {
        // Look at a few chords before the modulation point
        for (int i = Math.Max(0, modulationIndex - 3); i < modulationIndex; i++)
        {
            var chord = chords[i];

            // Try to analyze this chord in both keys
            var fromAnalysis = TryAnalyzeChordInKey(chord.PitchClasses, fromKey);
            var toAnalysis = TryAnalyzeChordInKey(chord.PitchClasses, toKey);

            if (fromAnalysis != null && toAnalysis != null)
            {
                return (fromAnalysis, toAnalysis);
            }
        }

        return null;
    }

    private static RomanNumeralChord? TryAnalyzeChordInKey(int[] pitchClasses, KeySignature key)
    {
        if (pitchClasses.Length < 2)
        {
            return null;
        }

        try
        {
            var scale = key.GetScale();
            var root = pitchClasses[0];

            // Check if root is in the scale
            if (!scale.Contains(root))
            {
                return null;
            }

            var scaleIndex = Array.IndexOf(scale, root);
            if (scaleIndex < 0)
            {
                return null;
            }

            // Create simple roman numeral description
            var scaleDegree = (ScaleDegree)(scaleIndex + 1);

            // Simplified - just return a basic roman numeral chord
            return new RomanNumeralChord(scaleDegree, ChordQuality.Major, HarmonicFunction.Tonic);
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeModulation(
        KeySignature fromKey,
        KeySignature toKey,
        ModulationType type,
        (RomanNumeralChord?, RomanNumeralChord?)? pivotChord)
    {
        var parts = new List<string>
        {
            $"{type} modulation from {fromKey} to {toKey}"
        };

        if (pivotChord.HasValue && pivotChord.Value.Item1 != null && pivotChord.Value.Item2 != null)
        {
            parts.Add($"via pivot chord {pivotChord.Value.Item1} = {pivotChord.Value.Item2}");
        }

        var interval = (toKey.Root - fromKey.Root + 12) % 12;
        var intervalName = interval switch
        {
            0 => "unison",
            1 => "minor second",
            2 => "major second",
            3 => "minor third",
            4 => "major third",
            5 => "perfect fourth",
            6 => "tritone",
            7 => "perfect fifth",
            8 => "minor sixth",
            9 => "major sixth",
            10 => "minor seventh",
            11 => "major seventh",
            _ => ""
        };

        if (!string.IsNullOrEmpty(intervalName))
        {
            parts.Add($"({intervalName} relationship)");
        }

        return string.Join(" ", parts);
    }
}

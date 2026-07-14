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
        // Whole-note time units (quarter = 1/4), so 2/1 is two whole notes (~two 4/4 bars,
        // 8 quarter-beats). A foreign-key area shorter than this counts as a tonicization.
        var minModulationDuration = new Rational(2, 1);

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

        // Number of chords to analyze at once; shrink for short inputs so pieces
        // with few chords are still analyzed from the first possible window.
        var windowSize = Math.Clamp(chords.Count / 2, 2, 8);

        // Deduplication of tonicization events: while stability holds, the same
        // target key would otherwise re-fire at every consecutive index.
        KeySignature? lastEmittedTarget = null;
        var lastEmittedIndex = int.MinValue;

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

            // Same target key still being detected within one analysis window of the last
            // sighting: extend the run instead of emitting a duplicate. A tolerance of
            // windowSize (rather than strict index adjacency) bridges the short gaps that
            // occur when a single index dips below the stability threshold, while a genuine
            // re-tonicization after a longer return home is still emitted as a new event.
            if (lastEmittedTarget is { } prevTarget
                && prevTarget.Root == detectedKey.Value.Root
                && prevTarget.IsMajor == detectedKey.Value.IsMajor
                && i - lastEmittedIndex <= windowSize)
            {
                lastEmittedIndex = i;
                continue;
            }

            // Determine modulation type
            var modulationType = DetermineModulationType(currentKey, detectedKey.Value, currentChord.Offset);
            var confidence = stability;

            // Check duration to distinguish tonicization from true modulation
            var duration = CalculateKeyDuration(chords, i, detectedKey.Value);
            var isTonicization = duration < minModulationDuration;

            modulationType = isTonicization switch
            {
                true => ModulationType.Tonicization,
                _ => modulationType
            };

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
            lastEmittedTarget = detectedKey.Value;
            lastEmittedIndex = i;

            currentKey = isTonicization switch
            {
                // Update current key if this is a true modulation
                false => detectedKey.Value,
                _ => currentKey
            };
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

            groups[quantizedOffset] = groups.ContainsKey(quantizedOffset) switch
            {
                false => [],
                _ => groups[quantizedOffset]
            };

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

        // Collect all pitch classes in the window and detect the key
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

        return interval switch
        {
            // Relative key (minor third apart, opposite modes)
            3 or 9 when fromKey.IsMajor != toKey.IsMajor => ModulationType.Direct,
            // Chromatic mediant (major or minor third, same mode)
            3 or 4 or 8 or 9 when fromKey.IsMajor == toKey.IsMajor => ModulationType.Chromatic,
            _ => ModulationType.Direct
        };

        // Default to direct or pivot chord (requires analysis of actual chords)
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
            // Identify the actual chord root and quality (pitchClasses[0] is just the
            // lowest pitch class, not the harmonic root).
            var info = ChordAnalyzer.Identify(pitchClasses);
            if (info.Quality == ChordQuality.Unknown)
            {
                return null;
            }

            var scale = key.GetScale();
            var scaleIndex = Array.IndexOf(scale, (int)info.RootPitchClass);
            if (scaleIndex < 0)
            {
                return null;
            }

            var scaleDegree = ScaleIndexToDegree(scaleIndex);
            var function = DegreeToFunction(scaleDegree);

            return new RomanNumeralChord(scaleDegree, info.Quality, function);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Map a diatonic scale index (0..6) to its ScaleDegree enum member.
    /// (The enum values are semitone offsets, so a plain cast is NOT valid.)
    /// </summary>
    private static ScaleDegree ScaleIndexToDegree(int scaleIndex) => scaleIndex switch
    {
        0 => ScaleDegree.I,
        1 => ScaleDegree.Ii,
        2 => ScaleDegree.Iii,
        3 => ScaleDegree.Iv,
        4 => ScaleDegree.V,
        5 => ScaleDegree.Vi,
        6 => ScaleDegree.Vii,
        _ => ScaleDegree.I
    };

    /// <summary>
    /// Harmonic function of a diatonic degree (same mapping KeyAnalyzer uses:
    /// I/iii/vi = Tonic, ii/IV = Subdominant, V/vii = Dominant).
    /// </summary>
    private static HarmonicFunction DegreeToFunction(ScaleDegree degree) => degree switch
    {
        ScaleDegree.I or ScaleDegree.Iii or ScaleDegree.Vi => HarmonicFunction.Tonic,
        ScaleDegree.Ii or ScaleDegree.Iv => HarmonicFunction.Subdominant,
        ScaleDegree.V or ScaleDegree.Vii => HarmonicFunction.Dominant,
        _ => HarmonicFunction.Tonic
    };

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

        if (pivotChord is { Item1: not null, Item2: not null })
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

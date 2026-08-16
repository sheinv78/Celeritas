// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Represents a detected key change or tonicization.
/// </summary>
public sealed class ModulationEvent
{
    // Produced by analysis; not constructible by consumers (#18 API freeze).
    internal ModulationEvent() { }

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
    // Produced by ModulationDetector; not constructible by consumers (#18 API freeze).
    internal ModulationAnalysisResult() { }

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
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static ModulationAnalysisResult Analyze(NoteBuffer buffer, KeySignature startKey)
    {
        ArgumentNullException.ThrowIfNull(buffer);

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
    /// <remarks>
    /// Harmonic evidence is normally taken from chords (2+ simultaneous onsets on an
    /// eighth-note grid). When the input is (nearly) monophonic and fewer than two such
    /// chords exist, the analysis falls back to treating each quantized onset as a
    /// pseudo-chord — single notes included — so melodic key changes are still detected.
    /// Pivot-chord identification is unavailable in that fallback.
    /// </remarks>
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
            var windowStart = i - windowSize;

            // Detect key at this position (evidence window is chords[windowStart..i-1])
            var detectedKey = DetectKeyInWindow(chords, windowStart, i, currentKey);

            if (detectedKey == null || detectedKey.Equals(currentKey))
            {
                continue;
            }

            // Check if this is a real modulation or just a passing chromaticism
            var futureEnd = Math.Min(i + (windowSize / 2), chords.Count);
            var stability = MeasureKeyStability(chords, i, futureEnd, detectedKey.Value);

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

            // The key change is confirmed at index i, but the evidence window is
            // chords[windowStart..i-1]: attributing the boundary to chords[i] lagged the
            // reported Offset by up to windowSize chords and truncated the measured new-key
            // area, biasing real modulations toward Tonicization. Attribute the boundary to
            // the earliest window chord that belongs to the new key and not the old one.
            var boundaryIndex = FindModulationBoundary(chords, windowStart, i, currentKey, detectedKey.Value);

            // Determine modulation type
            var modulationType = DetermineModulationType(currentKey, detectedKey.Value);
            var confidence = stability;

            // Check duration to distinguish tonicization from true modulation,
            // measured from the attributed boundary rather than the detection index.
            var duration = CalculateKeyDuration(chords, boundaryIndex, detectedKey.Value);
            var isTonicization = duration < minModulationDuration;

            modulationType = isTonicization switch
            {
                true => ModulationType.Tonicization,
                _ => modulationType
            };

            // Look for pivot chord
            var pivotChord = FindPivotChord(chords, i, currentKey, detectedKey.Value);

            // DetermineModulationType only sees the root interval, so it can never produce
            // PivotChord on its own. Direct is its generic fallback: when a pivot chord was
            // actually found, the more specific PivotChord classification applies. The
            // interval-specific labels (Chromatic, ModalInterchange) and Tonicization keep
            // priority over the pivot upgrade.
            if (modulationType == ModulationType.Direct && pivotChord != null)
            {
                modulationType = ModulationType.PivotChord;
            }

            var modulation = new ModulationEvent
            {
                Offset = chords[boundaryIndex].Offset,
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

        // Fallback for (nearly) monophonic input: with fewer than two simultaneous-onset
        // chords the analysis loop never runs and a melody's key change was silently
        // reported as "no modulations". Reuse the same eighth-note quantization groups,
        // but let every onset form a pseudo-chord — a single note becomes a 1-note event.
        // Pivot-chord identification still requires real (2+ note) chords and simply
        // yields none here; key detection and stability work fine on single notes.
        if (chords.Count < 2 && groups.Count > 0)
        {
            chords.Clear();
            foreach (var (offset, pitches) in groups.OrderBy(kvp => kvp.Key))
            {
                var mask = ChordAnalyzer.GetMask(pitches.ToArray());
                var pitchClasses = PitchClassSetAnalyzer.MaskToPitchClasses(mask);

                chords.Add(new ChordEvent(offset, mask, pitchClasses));
            }
        }

        return chords;
    }

    private static Rational QuantizeOffset(Rational offset, Rational grid)
    {
        var ratio = offset / grid;
        var rounded = (int)Math.Round(ratio.ToDouble());
        return grid * rounded;
    }

    private static KeySignature? DetectKeyInWindow(List<ChordEvent> chords, int start, int end, KeySignature currentKey)
    {
        if (start >= end)
        {
            return null;
        }

        // Collect all pitch classes in the window and detect the key
        var allPitches = new List<int>();
        for (int i = start; i < end; i++)
        {
            allPitches.AddRange(chords[i].PitchClasses);
        }

        if (allPitches.Count == 0)
        {
            return null;
        }

        var detectedKey = KeyAnalyzer.IdentifyKey([.. allPitches]);

        // Require significant difference from current key
        if (detectedKey.Root == currentKey.Root && detectedKey.IsMajor == currentKey.IsMajor)
        {
            return null;
        }

        return detectedKey;
    }

    private static float MeasureKeyStability(List<ChordEvent> chords, int start, int end, KeySignature key)
    {
        if (start >= end)
        {
            return 0f;
        }

        var scale = key.GetScale();
        var inKeyCount = 0;
        var totalCount = 0;

        for (int i = start; i < end; i++)
        {
            foreach (var pc in chords[i].PitchClasses)
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

    /// <summary>
    /// Locate the chord where a confirmed key change actually begins. Scans the evidence
    /// window (chords[windowStart..detectionIndex-1]) for the earliest chord that is
    /// diatonic to the new key and NOT diatonic to the old key — the first unambiguous
    /// new-key sonority. Falls back to the window start when every window chord is
    /// ambiguous (diatonic to both keys or to neither).
    /// </summary>
    private static int FindModulationBoundary(
        List<ChordEvent> chords,
        int windowStart,
        int detectionIndex,
        KeySignature fromKey,
        KeySignature toKey)
    {
        var fromScale = fromKey.GetScale();
        var toScale = toKey.GetScale();

        for (int i = windowStart; i < detectionIndex; i++)
        {
            var pcs = chords[i].PitchClasses;
            if (pcs.Length == 0)
            {
                continue;
            }

            var diatonicToNew = pcs.All(pc => toScale.Contains(pc));
            var diatonicToOld = pcs.All(pc => fromScale.Contains(pc));

            if (diatonicToNew && !diatonicToOld)
            {
                return i;
            }
        }

        return windowStart;
    }

    private static ModulationType DetermineModulationType(KeySignature fromKey, KeySignature toKey)
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

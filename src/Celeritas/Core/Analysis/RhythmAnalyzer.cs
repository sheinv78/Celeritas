// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Beat strength in a measure.
/// </summary>
public enum BeatStrength
{
    /// <summary>Strongest beat (downbeat, beat 1).</summary>
    Strong,

    /// <summary>Secondary strong beat (e.g., beat 3 in 4/4).</summary>
    Medium,

    /// <summary>Weak beat (off-beats).</summary>
    Weak,

    /// <summary>Subdivision of a beat.</summary>
    Subdivision
}

/// <summary>
/// High-level groove feel classification.
/// </summary>
public enum GrooveFeel
{
    /// <summary>Even, unswung subdivisions.</summary>
    Straight,

    /// <summary>Swung eighths (moderate long-short feel).</summary>
    Swing,

    /// <summary>Heavy triplet-based long-short feel.</summary>
    Shuffle,

    /// <summary>Latin/Afro-Cuban clave-driven feel.</summary>
    Latin,

    /// <summary>Compound meter feel (beats divide in three).</summary>
    Compound
}

/// <summary>
/// A rhythmic event (onset) with metrical position.
/// </summary>
public readonly record struct RhythmEvent
{
    /// <summary>Onset time of the event, in whole-note units.</summary>
    public Rational Offset { get; init; }

    /// <summary>Duration of the event, in whole-note units.</summary>
    public Rational Duration { get; init; }

    /// <summary>Zero-based index of the measure containing the onset.</summary>
    public int Measure { get; init; }

    /// <summary>Position within the measure, in whole-note units from the barline.</summary>
    public Rational BeatInMeasure { get; init; }

    /// <summary>Metrical strength of the onset position.</summary>
    public BeatStrength Strength { get; init; }

    /// <summary>Whether the note is syncopated (weak-beat onset sustained over the next strong beat).</summary>
    public bool IsSyncopated { get; init; }

    /// <summary>Index of the note in the original input buffer.</summary>
    public int OriginalIndex { get; init; }

    /// <summary>End time of the event (<c>Offset + Duration</c>), in whole-note units.</summary>
    public Rational End => Offset + Duration;
}

/// <summary>
/// A recognized rhythmic pattern.
/// </summary>
public sealed class RhythmPattern
{
    /// <summary>Name of the pattern.</summary>
    public required string Name { get; init; }

    /// <summary>Duration pattern as rationals.</summary>
    public required Rational[] Durations { get; init; }


    /// <summary>Style/genre association.</summary>
    public string? Style { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Returns the pattern name.</summary>
    public override string ToString() => Name;
}

/// <summary>
/// Result of meter detection.
/// </summary>
public sealed record MeterDetectionResult
{
    /// <summary>Most likely detected time signature.</summary>
    public required TimeSignature TimeSignature { get; init; }

    /// <summary>Confidence of the detection, in the range 0-1.</summary>
    public required float Confidence { get; init; }

    /// <summary>Estimated tempo in beats per minute (placeholder without audio input).</summary>
    public required Rational Tempo { get; init; }

    /// <summary>Other plausible time signatures, best first.</summary>
    public required IReadOnlyList<TimeSignature> Alternatives { get; init; }

    /// <summary>Human-readable explanation of the detection outcome.</summary>
    public required string Reasoning { get; init; }
}

/// <summary>
/// Complete rhythm analysis result.
/// </summary>
public sealed record RhythmAnalysisResult
{
    /// <summary>Detected or supplied meter.</summary>
    public required MeterDetectionResult Meter { get; init; }

    /// <summary>Per-onset rhythmic events in metrical context, ordered by time.</summary>
    public required IReadOnlyList<RhythmEvent> Events { get; init; }

    /// <summary>Recognized rhythmic pattern occurrences.</summary>
    public required IReadOnlyList<RhythmPatternMatch> PatternMatches { get; init; }

    /// <summary>Aggregate rhythmic statistics.</summary>
    public required RhythmStatistics Statistics { get; init; }

    /// <summary>Swing ratio: fraction of a beat-pair taken by the first note (0.5 = straight).</summary>
    public required float SwingRatio { get; init; }

    /// <summary>Fraction of notes that are syncopated, in the range 0-1.</summary>
    public required float Syncopation { get; init; }

    /// <summary>Rhythmic density: onsets per beat.</summary>
    public required float Density { get; init; }

    /// <summary>High-level groove feel classification.</summary>
    public required GrooveFeel GrooveFeel { get; init; }

    /// <summary>Rhythmic drive/energy score, in the range 0-1.</summary>
    public required float GrooveDrive { get; init; }

    /// <summary>Human-readable description of the rhythmic texture.</summary>
    public required string TextureDescription { get; init; }
}

/// <summary>
/// A matched rhythmic pattern with location.
/// </summary>
public sealed record RhythmPatternMatch
{
    /// <summary>The pattern that was matched.</summary>
    public required RhythmPattern Pattern { get; init; }

    /// <summary>Onset time where the match begins, in whole-note units.</summary>
    public required Rational StartOffset { get; init; }

    /// <summary>Index of the first onset of the match within the sorted note sequence.</summary>
    public required int StartIndex { get; init; }

    /// <summary>Number of onsets spanned by the match.</summary>
    public required int Count { get; init; }

    /// <summary>Match quality, in the range 0-1 (1 = exact).</summary>
    public required float MatchQuality { get; init; }
}

/// <summary>
/// Statistics about rhythmic features.
/// </summary>
public sealed record RhythmStatistics
{
    /// <summary>Total number of notes analyzed.</summary>
    public int TotalNotes { get; init; }

    /// <summary>Number of measures spanned by the notes.</summary>
    public int MeasureCount { get; init; }

    /// <summary>Average number of notes per measure.</summary>
    public float NotesPerMeasure { get; init; }

    /// <summary>Shortest note duration, in whole-note units.</summary>
    public Rational ShortestDuration { get; init; }

    /// <summary>Longest note duration, in whole-note units.</summary>
    public Rational LongestDuration { get; init; }

    /// <summary>Mean note duration, in whole-note units.</summary>
    public Rational AverageDuration { get; init; }

    /// <summary>Number of syncopated notes.</summary>
    public int SyncopatedNotes { get; init; }

    /// <summary>Percentage of notes that are syncopated; 0 when there are no notes.</summary>
    public float SyncopationPercent => TotalNotes > 0 ? SyncopatedNotes * 100f / TotalNotes : 0;

    /// <summary>Count of notes keyed by their duration (whole-note units).</summary>
    public Dictionary<Rational, int> DurationHistogram { get; init; } = [];

    /// <summary>Count of onsets keyed by their beat strength.</summary>
    public Dictionary<BeatStrength, int> StrengthHistogram { get; init; } = [];
}

/// <summary>
/// Rhythm analyzer - meter detection, pattern recognition, syncopation.
/// </summary>
public static class RhythmAnalyzer
{
    /// <summary>
    /// Common rhythmic patterns for recognition.
    /// </summary>
    public static readonly RhythmPattern[] CommonPatterns =
    [
        new RhythmPattern
        {
            Name = "Straight Quarters",
            Durations = [new(1,4), new(1,4), new(1,4), new(1,4)],
            Style = "Classical",
            Description = "Even quarter notes"
        },
        new RhythmPattern
        {
            Name = "Straight Eighths",
            Durations = [new(1,8), new(1,8), new(1,8), new(1,8), new(1,8), new(1,8), new(1,8), new(1,8)],
            Style = "Various",
            Description = "Even eighth notes"
        },
        new RhythmPattern
        {
            Name = "Dotted Quarter-Eighth",
            Durations = [new(3,8), new(1,8)],
            Style = "Various",
            Description = "Long-short pattern"
        },
        new RhythmPattern
        {
            Name = "Habanera",
            Durations = [new(3,8), new(1,8), new(1,4), new(1,4)],
            Style = "Latin",
            Description = "Dotted rhythm + quarters"
        },
        new RhythmPattern
        {
            Name = "Tresillo",
            Durations = [new(3,8), new(3,8), new(2,8)],
            Style = "Afro-Cuban",
            Description = "3+3+2 pattern"
        },
        new RhythmPattern
        {
            Name = "Clave 3-2",
            Durations = [new(3,8), new(3,8), new(2,8), new(2,8), new(2,8), new(4,8)],
            Style = "Afro-Cuban",
            Description = "Son clave pattern"
        },
        new RhythmPattern
        {
            Name = "Backbeat",
            Durations = [new(1,4), new(1,4), new(1,4), new(1,4)],
            Style = "Rock/Pop",
            Description = "Accent on 2 and 4"
        },
        new RhythmPattern
        {
            Name = "Shuffle",
            Durations = [new(2,12), new(1,12), new(2,12), new(1,12), new(2,12), new(1,12), new(2,12), new(1,12)],
            Style = "Blues/Jazz",
            Description = "Swung triplet feel"
        },
        new RhythmPattern
        {
            Name = "Sixteenths",
            Durations = [new(1,16), new(1,16), new(1,16), new(1,16)],
            Style = "Various",
            Description = "Even sixteenth notes"
        },
        new RhythmPattern
        {
            Name = "Syncopated",
            Durations = [new(1,8), new(1,4), new(1,8)],
            Style = "Jazz/Funk",
            Description = "Off-beat accent"
        },
        new RhythmPattern
        {
            Name = "Charleston",
            Durations = [new(3,8), new(1,8), new(1,4)],
            Style = "Jazz",
            Description = "Dotted-eighth-sixteenth-quarter"
        },
        new RhythmPattern
        {
            Name = "Triplet",
            Durations = [new(1,12), new(1,12), new(1,12)],
            Style = "Various",
            Description = "Three equal notes in beat"
        },
        new RhythmPattern
        {
            Name = "Waltz",
            Durations = [new(1,4), new(1,4), new(1,4)],
            Style = "Classical",
            Description = "Three-beat pattern"
        }
    ];

    /// <summary>
    /// Detect the most likely time signature from a sequence of notes.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static MeterDetectionResult DetectMeter(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer.Count == 0)
        {
            return new MeterDetectionResult
            {
                TimeSignature = TimeSignature.Common,
                Confidence = 0.5f,
                Tempo = new Rational(120, 1),
                Alternatives = [],
                Reasoning = "No notes provided"
            };
        }

        var onsets = CollectSortedOnsets(buffer);

        return DetectMeterInternal(onsets, CollectVelocities(buffer));
    }

    /// <summary>
    /// Detect the most likely time signature from a sequence of note events.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="notes"/> is <see langword="null"/>.</exception>
    public static MeterDetectionResult DetectMeter(IEnumerable<NoteEvent> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var arr = notes as NoteEvent[] ?? [.. notes];
        using var buffer = new NoteBuffer(Math.Max(4, arr.Length));
        buffer.AddRange(arr);
        return DetectMeter(buffer);
    }

    /// <summary>
    /// Identify rhythmic pattern in a sequence of notes.
    /// Returns the best matching pattern with quality score.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static RhythmPatternMatch? IdentifyPattern(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer.Count == 0)
            return null;

        var onsets = CollectSortedOnsets(buffer);
        var velocities = CollectVelocities(buffer);

        var meter = DetectMeterInternal(onsets, velocities).TimeSignature;
        var matches = DetectPatterns(onsets, meter, velocities);
        return matches.OrderByDescending(m => m.MatchQuality).FirstOrDefault();
    }

    /// <summary>
    /// Identify rhythmic pattern in a sequence of note events.
    /// Returns the best matching pattern with quality score.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="notes"/> is <see langword="null"/>.</exception>
    public static RhythmPatternMatch? IdentifyPattern(IEnumerable<NoteEvent> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var arr = notes as NoteEvent[] ?? [.. notes];
        using var buffer = new NoteBuffer(Math.Max(4, arr.Length));
        buffer.AddRange(arr);
        return IdentifyPattern(buffer);
    }

    /// <summary>
    /// Analyze rhythm of a note buffer.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static RhythmAnalysisResult Analyze(NoteBuffer buffer, TimeSignature? knownMeter = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer.Count == 0)
        {
            return EmptyResult();
        }

        // Collect onsets in deterministic order
        var onsets = CollectSortedOnsets(buffer);
        var velocities = CollectVelocities(buffer);

        // Detect or use known meter
        var meter = knownMeter.HasValue
            ? new MeterDetectionResult
            {
                TimeSignature = knownMeter.Value,
                Confidence = 1.0f,
                Tempo = new Rational(120, 1),
                Alternatives = [],
                Reasoning = "User-specified meter"
            }
            : DetectMeterInternal(onsets, velocities);

        // Analyze each event in metrical context
        var events = AnalyzeEvents(onsets, meter.TimeSignature);

        // Detect patterns
        var patterns = DetectPatterns(onsets, meter.TimeSignature, velocities);

        // Calculate statistics
        var stats = CalculateStatistics(events);

        // Detect swing
        var swing = DetectSwing(onsets);

        // Calculate syncopation level
        var syncopation = CalculateSyncopation(events);

        // Calculate density
        var density = CalculateDensity(onsets, meter.TimeSignature);

        // Generate texture description
        var texture = DescribeTexture(swing, syncopation, density, patterns);

        var grooveFeel = DetermineGrooveFeel(meter.TimeSignature, swing, patterns);
        var grooveDrive = CalculateGrooveDrive(stats, density, syncopation, swing, grooveFeel);

        return new RhythmAnalysisResult
        {
            Meter = meter,
            Events = events,
            PatternMatches = patterns,
            Statistics = stats,
            SwingRatio = swing,
            Syncopation = syncopation,
            Density = density,
            GrooveFeel = grooveFeel,
            GrooveDrive = grooveDrive,
            TextureDescription = texture
        };
    }

    private static RhythmAnalysisResult EmptyResult() => new()
    {
        Meter = new MeterDetectionResult
        {
            TimeSignature = TimeSignature.Common,
            Confidence = 0,
            Tempo = new Rational(120, 1),
            Alternatives = [],
            Reasoning = "No notes"
        },
        Events = [],
        PatternMatches = [],
        Statistics = new RhythmStatistics(),
        SwingRatio = 0.5f,
        Syncopation = 0,
        Density = 0,
        GrooveFeel = GrooveFeel.Straight,
        GrooveDrive = 0,
        TextureDescription = "No rhythmic content"
    };

    /// <summary>Score margin below which two meters are considered tied.</summary>
    private const float MeterTieEpsilon = 0.01f;

    /// <summary>
    /// Collects (offset, duration, buffer index) tuples sorted by onset time with a
    /// pitch (then index) tie-break, so simultaneous chord notes are ordered
    /// deterministically regardless of buffer insertion order.
    /// </summary>
    private static List<(Rational offset, Rational duration, int index)> CollectSortedOnsets(NoteBuffer buffer)
    {
        // An onset is something being struck, and a rest is the absence of one. Counted as
        // onsets, rests halved the note density and turned 2/2 into 2/4. The index is kept as
        // the buffer index so it still addresses the parallel velocity array.
        var onsets = new List<(Rational offset, Rational duration, int index)>(buffer.Count);
        for (int i = 0; i < buffer.Count; i++)
        {
            if (Rests.IsRest(buffer.PitchAt(i))) continue;
            onsets.Add((buffer.GetOffset(i), buffer.GetDuration(i), i));
        }

        onsets.Sort((a, b) =>
        {
            var cmp = a.offset.CompareTo(b.offset);
            if (cmp != 0)
                return cmp;
            cmp = buffer.PitchAt(a.index).CompareTo(buffer.PitchAt(b.index));
            return cmp != 0 ? cmp : a.index.CompareTo(b.index);
        });

        return onsets;
    }

    private static float[] CollectVelocities(NoteBuffer buffer)
    {
        var velocities = new float[buffer.Count];
        for (int i = 0; i < buffer.Count; i++)
        {
            velocities[i] = buffer.GetVelocity(i);
        }

        return velocities;
    }

    private static MeterDetectionResult DetectMeterInternal(
        List<(Rational offset, Rational duration, int index)> onsets,
        float[] velocities)
    {
        if (onsets.Count < 2)
        {
            return new MeterDetectionResult
            {
                TimeSignature = TimeSignature.Common,
                Confidence = 0.5f,
                Tempo = new Rational(120, 1),
                Alternatives = [TimeSignature.Waltz, TimeSignature.CutTime],
                Reasoning = "Insufficient data, defaulting to 4/4"
            };
        }

        // Calculate inter-onset intervals (IOIs) - pre-allocate
        var iois = new List<Rational>(onsets.Count - 1);
        for (int i = 1; i < onsets.Count; i++)
        {
            var ioi = onsets[i].offset - onsets[i - 1].offset;
            if (ioi > Rational.Zero)
                iois.Add(ioi);
        }

        if (iois.Count == 0)
        {
            return new MeterDetectionResult
            {
                TimeSignature = TimeSignature.Common,
                Confidence = 0.5f,
                Tempo = new Rational(120, 1),
                Alternatives = [],
                Reasoning = "No intervals detected"
            };
        }

        // The most common IOI approximates the surface pulse. It anchors the accent
        // model: an onset arriving after a gap well beyond the pulse (following a
        // long note or a rest) is heard as accented.
        var ioiCounts = new Dictionary<Rational, int>();
        foreach (var ioi in iois)
        {
            ioiCounts.TryGetValue(ioi, out var count);
            ioiCounts[ioi] = count + 1;
        }

        var commonIoi = ioiCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key.ToDouble())
            .First().Key;

        var accentWeights = ComputeAccentWeights(onsets, velocities, commonIoi);

        // Score different meters
        var meters = new[]
        {
            TimeSignature.Common,
            TimeSignature.Waltz,
            TimeSignature.CutTime,
            TimeSignature.Compound6,
            new TimeSignature(2, 4),
            new TimeSignature(6, 4)
        };

        var scores = new Dictionary<TimeSignature, float>();
        foreach (var m in meters)
        {
            scores[m] = ScoreMeter(onsets, m, accentWeights);
        }

        // Accent-free (uniform) input fits every meter equally well by construction,
        // so ties are common; resolve them with a deterministic preference for the
        // least surprising meters: 4/4, then 3/4, 6/8, 2/4, then the rest.
        var bestScore = scores.Values.Max();
        var best = meters
            .Where(m => scores[m] >= bestScore - MeterTieEpsilon)
            .OrderBy(MeterPreferenceRank)
            .First();
        var bestConfidence = scores[best];

        var alternatives = meters
            .Where(m => m != best && scores[m] > 0.3f)
            .OrderByDescending(m => scores[m])
            .ThenBy(MeterPreferenceRank)
            .Take(3)
            .ToList();

        var reasoning = best.IsCompound
            ? "Compound meter detected - notes group in threes"
            : "Simple meter - beats divide in two";

        if (bestConfidence < 0.5f)
            reasoning += " (low confidence)";

        return new MeterDetectionResult
        {
            TimeSignature = best,
            Confidence = bestConfidence,
            Tempo = new Rational(120, 1), // Would need audio for real tempo
            Alternatives = alternatives,
            Reasoning = reasoning
        };
    }

    private static int MeterPreferenceRank(TimeSignature meter) => (meter.BeatsPerMeasure, meter.BeatUnit) switch
    {
        (4, 4) => 0,
        (3, 4) => 1,
        (6, 8) => 2,
        (2, 4) => 3,
        _ => 4
    };

    /// <summary>
    /// Perceptual accent weight per onset. Longer-than-average notes, louder-than-average
    /// notes (only when velocities actually vary), and onsets entering after a gap well
    /// beyond the common pulse all read as accents. Uniform input yields uniform weights.
    /// </summary>
    private static double[] ComputeAccentWeights(
        List<(Rational offset, Rational duration, int index)> onsets,
        float[] velocities,
        Rational commonIoi)
    {
        var weights = new double[onsets.Count];

        double durationSum = 0, velocitySum = 0;
        var minVelocity = float.MaxValue;
        var maxVelocity = float.MinValue;
        foreach (var (_, duration, index) in onsets)
        {
            durationSum += duration.ToDouble();
            var velocity = velocities[index];
            velocitySum += velocity;
            minVelocity = Math.Min(minVelocity, velocity);
            maxVelocity = Math.Max(maxVelocity, velocity);
        }

        var meanDuration = durationSum / onsets.Count;
        var meanVelocity = velocitySum / onsets.Count;
        var velocityVaries = maxVelocity - minVelocity > 0.001f;
        var gapThreshold = commonIoi.ToDouble() * 1.5;

        for (int i = 0; i < onsets.Count; i++)
        {
            double weight = 1;
            if (meanDuration > 0)
                weight *= Math.Clamp(onsets[i].duration.ToDouble() / meanDuration, 0.5, 2.0);
            if (velocityVaries && meanVelocity > 0)
                weight *= Math.Clamp(velocities[onsets[i].index] / meanVelocity, 0.5, 2.0);
            if (i > 0 && (onsets[i].offset - onsets[i - 1].offset).ToDouble() >= gapThreshold)
                weight *= 1.5;
            weights[i] = weight;
        }

        return weights;
    }

    private static float ScoreMeter(
        List<(Rational offset, Rational duration, int index)> onsets,
        TimeSignature meter,
        double[] accentWeights)
    {
        // Raw strength sums are not comparable across meters: a meter with a denser
        // strong-beat grid (2/4 vs 4/4, 6/8 vs 3/4) scores higher on ANY input, so
        // 4/4 and 3/4 could never win. Instead measure whether ACCENTED onsets sit
        // on metrically strong positions: the accent-weighted mean strength minus
        // the unweighted mean over the same onsets. Accent-free input scores the
        // baseline 0.5 for every meter (a tie, resolved by the preference order);
        // accents on strong beats push the score above 0.5, contradicting accents
        // push it below. The result is a meaningful 0-1 confidence.
        var measureDur = meter.MeasureDuration;
        double weightedSum = 0, weightSum = 0, plainSum = 0;

        for (int i = 0; i < onsets.Count; i++)
        {
            var measurePos = GetPositionInMeasure(onsets[i].offset, measureDur);
            var strength = GetBeatStrength(measurePos, meter) switch
            {
                BeatStrength.Strong => 1.0,
                BeatStrength.Medium => 0.6,
                BeatStrength.Weak => 0.3,
                _ => 0.1
            };

            weightedSum += accentWeights[i] * strength;
            weightSum += accentWeights[i];
            plainSum += strength;
        }

        var accentAlignment = (weightedSum / weightSum) - (plainSum / onsets.Count);
        return Math.Clamp(0.5f + (float)accentAlignment, 0f, 1f);
    }

    private static Rational GetPositionInMeasure(Rational offset, Rational measureDuration)
    {
        // offset mod measureDuration
        var measures = (long)(offset.ToDouble() / measureDuration.ToDouble());
        return offset - (measureDuration * measures);
    }

    private static BeatStrength GetBeatStrength(Rational posInMeasure, TimeSignature meter)
    {
        var beatDur = meter.BeatDuration;
        var posDouble = posInMeasure.ToDouble();
        var beatDouble = beatDur.ToDouble();

        // Check if on a beat
        var beatNumber = posDouble / beatDouble;
        var isOnBeat = Math.Abs(beatNumber - Math.Round(beatNumber)) < 0.01;

        if (!isOnBeat)
            return BeatStrength.Subdivision;

        var beat = (int)Math.Round(beatNumber);

        return beat switch
        {
            // Beat 0 (downbeat) is always strong
            0 => BeatStrength.Strong,
            _ => meter.BeatsPerMeasure switch
            {
                // In 4/4, beat 2 (third beat) is medium
                4 when beat == 2 => BeatStrength.Medium,
                _ => meter.IsCompound switch
                {
                    // In compound meters, beats 0, 3, 6, 9 are strong
                    true when beat % 3 == 0 => beat == 0 ? BeatStrength.Strong : BeatStrength.Medium,
                    _ => BeatStrength.Weak
                }
            }
        };
    }

    private static List<RhythmEvent> AnalyzeEvents(
        List<(Rational offset, Rational duration, int index)> onsets,
        TimeSignature meter)
    {
        var events = new List<RhythmEvent>(onsets.Count);
        var measureDur = meter.MeasureDuration;

        foreach (var (offset, duration, index) in onsets)
        {
            var measure = (int)(offset.ToDouble() / measureDur.ToDouble());
            var posInMeasure = GetPositionInMeasure(offset, measureDur);
            var strength = GetBeatStrength(posInMeasure, meter);

            // Detect syncopation: note on weak beat that ties over the next strong beat
            var isSyncopated = IsSyncopated(strength, offset, duration, meter);

            events.Add(new RhythmEvent
            {
                Offset = offset,
                Duration = duration,
                Measure = measure,
                BeatInMeasure = posInMeasure,
                Strength = strength,
                IsSyncopated = isSyncopated,
                OriginalIndex = index
            });
        }

        return events;
    }

    private static Rational GetNextStrongBeat(Rational offset, TimeSignature meter)
    {
        var beatDur = meter.BeatDuration;
        var measureDur = meter.MeasureDuration;
        var currentBeat = (long)(offset.ToDouble() / beatDur.ToDouble());

        // Scan forward for the next metrically STRONG position (Strong or Medium).
        // Returning just the next beat of any strength made every weak-beat note
        // longer than a beat "syncopated" (e.g. a half note on beat 2 of 3/4).
        for (var beat = currentBeat + 1; beat <= currentBeat + meter.BeatsPerMeasure + 1; beat++)
        {
            var beatTime = beatDur * beat;
            var strength = GetBeatStrength(GetPositionInMeasure(beatTime, measureDur), meter);
            if (strength is BeatStrength.Strong or BeatStrength.Medium)
                return beatTime;
        }

        // Unreachable for well-formed meters (every measure has a strong downbeat);
        // fall back to the next downbeat.
        var currentMeasure = (long)(offset.ToDouble() / measureDur.ToDouble());
        return measureDur * (currentMeasure + 1);
    }

    private static bool IsSyncopated(BeatStrength strength, Rational offset, Rational duration, TimeSignature meter)
    {
        if (strength is not (BeatStrength.Weak or BeatStrength.Subdivision))
            return false;
        var noteEnd = offset + duration;
        var nextStrong = GetNextStrongBeat(offset, meter);
        return noteEnd.CompareTo(nextStrong) > 0;
    }

    private static List<RhythmPatternMatch> DetectPatterns(
        List<(Rational offset, Rational duration, int index)> onsets,
        TimeSignature meter,
        float[] velocities)
    {
        // Estimate: patterns are typically 4-8 notes, not many matches expected
        var matches = new List<RhythmPatternMatch>(onsets.Count / 4);

        foreach (var pattern in CommonPatterns)
        {
            // Slide pattern over onsets
            for (int i = 0; i <= onsets.Count - pattern.Durations.Length; i++)
            {
                if (!MeetsPatternRequirements(pattern, onsets, i, meter, velocities))
                    continue;

                var quality = MatchPattern(onsets, i, pattern);
                if (quality > 0.8f)
                {
                    matches.Add(new RhythmPatternMatch
                    {
                        Pattern = pattern,
                        StartOffset = onsets[i].offset,
                        StartIndex = i,
                        Count = pattern.Durations.Length,
                        MatchQuality = quality
                    });
                }
            }
        }

        // Remove overlapping matches: keep the best quality and, on equal quality,
        // the most specific pattern (Waltz/Backbeat carry extra metric/velocity
        // requirements that duration-identical Straight Quarters lacks).
        matches = [.. matches
            .GroupBy(m => m.StartIndex / 4) // Group by approximate position
            .Select(g => g
                .OrderByDescending(m => m.MatchQuality)
                .ThenByDescending(m => PatternSpecificity(m.Pattern))
                .First())];

        return matches;
    }

    private static int PatternSpecificity(RhythmPattern pattern) => pattern.Name switch
    {
        "Waltz" or "Backbeat" => 1,
        _ => 0
    };

    /// <summary>
    /// Extra requirements for patterns whose duration sequences alone are ambiguous
    /// (both are duration-identical to Straight Quarters and could otherwise never win):
    /// Waltz is quarter-quarter-quarter ONLY in 3/4 aligned to beats 1-2-3, and Backbeat
    /// is four quarters ONLY in 4/4 with velocity accents on beats 2 and 4 (with uniform
    /// velocities, Backbeat is simply not reported).
    /// </summary>
    private static bool MeetsPatternRequirements(
        RhythmPattern pattern,
        List<(Rational offset, Rational duration, int index)> onsets,
        int startIndex,
        TimeSignature meter,
        float[] velocities)
    {
        switch (pattern.Name)
        {
            case "Waltz":
                {
                    if (meter.BeatsPerMeasure != 3 || meter.BeatUnit != 4)
                        return false;
                    return OnsetsAlignToBeats(onsets, startIndex, 3, meter);
                }

            case "Backbeat":
                {
                    if (meter.BeatsPerMeasure != 4 || meter.BeatUnit != 4)
                        return false;
                    if (!OnsetsAlignToBeats(onsets, startIndex, 4, meter))
                        return false;

                    const float accentMargin = 0.01f;
                    var v1 = velocities[onsets[startIndex].index];
                    var v2 = velocities[onsets[startIndex + 1].index];
                    var v3 = velocities[onsets[startIndex + 2].index];
                    var v4 = velocities[onsets[startIndex + 3].index];
                    return v2 > v1 + accentMargin && v4 > v3 + accentMargin;
                }

            default:
                return true;
        }
    }

    /// <summary>Whether <paramref name="count"/> onsets from <paramref name="startIndex"/> sit exactly on beats 1..count of a measure.</summary>
    private static bool OnsetsAlignToBeats(
        List<(Rational offset, Rational duration, int index)> onsets,
        int startIndex,
        int count,
        TimeSignature meter)
    {
        var measureDur = meter.MeasureDuration;
        var beatDur = meter.BeatDuration;
        for (int k = 0; k < count; k++)
        {
            if (GetPositionInMeasure(onsets[startIndex + k].offset, measureDur) != beatDur * k)
                return false;
        }

        return true;
    }

    private static float MatchPattern(
        List<(Rational offset, Rational duration, int index)> onsets,
        int startIndex,
        RhythmPattern pattern)
    {
        if (startIndex + pattern.Durations.Length > onsets.Count)
            return 0;

        float totalError = 0;
        for (int i = 0; i < pattern.Durations.Length; i++)
        {
            var expected = pattern.Durations[i];
            var actual = onsets[startIndex + i].duration;

            var error = Math.Abs(expected.ToDouble() - actual.ToDouble());
            totalError += (float)error;
        }

        var avgError = totalError / pattern.Durations.Length;
        return Math.Max(0, 1.0f - (avgError * 4));
    }

    private static RhythmStatistics CalculateStatistics(
        List<RhythmEvent> events)
    {
        if (events.Count == 0)
            return new RhythmStatistics();

        var durations = events.Select(e => e.Duration).ToList();
        var durationHist = new Dictionary<Rational, int>();
        foreach (var d in durations)
        {
            durationHist.TryGetValue(d, out var count);
            durationHist[d] = count + 1;
        }

        var strengthHist = new Dictionary<BeatStrength, int>();
        foreach (var e in events)
        {
            strengthHist.TryGetValue(e.Strength, out var count);
            strengthHist[e.Strength] = count + 1;
        }

        var measureCount = events.Max(e => e.Measure) + 1;

        // Exact mean via Rational arithmetic (numerators cannot simply be summed
        // across different denominators).
        var durationSum = Rational.Zero;
        foreach (var d in durations)
        {
            durationSum += d;
        }
        var avgDur = durationSum / durations.Count;

        return new RhythmStatistics
        {
            TotalNotes = events.Count,
            MeasureCount = measureCount,
            NotesPerMeasure = (float)events.Count / measureCount,
            ShortestDuration = durations.MinBy(d => d.ToDouble()),
            LongestDuration = durations.MaxBy(d => d.ToDouble()),
            AverageDuration = avgDur,
            SyncopatedNotes = events.Count(e => e.IsSyncopated),
            DurationHistogram = durationHist,
            StrengthHistogram = strengthHist
        };
    }

    private static float DetectSwing(List<(Rational offset, Rational duration, int index)> onsets)
    {
        // Group onsets into quarter-note beats and pair the on-beat onset with the
        // following offbeat onset in the same beat. Pairing by absolute even/odd
        // index breaks as soon as a pickup or chord shifts the parity: every
        // subsequent pair inverts and swung music reads as "reverse swing".
        var beatDur = Rational.Quarter;
        var beatDurDouble = beatDur.ToDouble();
        var ratios = new List<double>();

        int i = 0;
        while (i < onsets.Count)
        {
            var beatIndex = (long)Math.Floor(onsets[i].offset.ToDouble() / beatDurDouble);
            var beatStart = beatDur * beatIndex;
            var beatEnd = beatDur * (beatIndex + 1);

            // Collect all onsets inside this beat.
            int j = i;
            while (j < onsets.Count && onsets[j].offset < beatEnd)
                j++;

            // A swing pair is exactly two onsets: one on the beat, one after it.
            // Beats with any other onset count carry no swing information.
            if (j - i == 2 &&
                onsets[i].offset == beatStart &&
                onsets[i + 1].offset > onsets[i].offset)
            {
                ratios.Add((onsets[i + 1].offset - beatStart).ToDouble() / beatDurDouble);
            }

            i = j;
        }

        if (ratios.Count == 0)
            return 0.5f; // No swing pairs detected (straight)

        return (float)Math.Clamp(ratios.Average(), 0.0, 1.0);
    }

    private static float CalculateSyncopation(List<RhythmEvent> events)
    {
        return events.Count switch
        {
            0 => 0,
            _ => events.Count(e => e.IsSyncopated) / (float)events.Count
        };
    }

    private static float CalculateDensity(
        List<(Rational offset, Rational duration, int index)> onsets,
        TimeSignature meter)
    {
        if (onsets.Count < 2) return 0;

        var totalDuration = onsets[^1].offset + onsets[^1].duration - onsets[0].offset;
        var measures = totalDuration.ToDouble() / meter.MeasureDuration.ToDouble();

        if (measures <= 0)
            return 0;

        return (float)(onsets.Count / measures / meter.BeatsPerMeasure);
    }

    private static string DescribeTexture(
        float swing,
        float syncopation,
        float density,
        List<RhythmPatternMatch> patterns)
    {
        var parts = new List<string>
        {
            // Density description
            density switch
            {
                < 0.5f => "Sparse, spacious rhythm",
                < 1.0f => "Moderate rhythmic activity",
                < 2.0f => "Active, driving rhythm",
                _ => "Dense, busy rhythmic texture"
            }
        };

        // Swing description
        if (swing is > 0.55f and < 0.75f)
            parts.Add($"with light swing ({(int)Math.Round(swing * 100)}% ratio)");
        else if (swing >= 0.75f)
            parts.Add($"with heavy swing/shuffle ({(int)Math.Round(swing * 100)}% ratio)");

        // Syncopation
        if (syncopation > 0.3f)
            parts.Add($"highly syncopated ({(int)Math.Round(syncopation * 100)}%)");
        else if (syncopation > 0.1f)
            parts.Add("with some syncopation");

        // Pattern mentions
        var mainPattern = patterns.OrderByDescending(p => p.MatchQuality).FirstOrDefault();
        if (mainPattern != null)
        {
            parts.Add($"featuring {mainPattern.Pattern.Name} pattern");
            if (mainPattern.Pattern.Style != null)
                parts.Add($"({mainPattern.Pattern.Style} style)");
        }

        return string.Join(", ", parts) + ".";
    }

    private static GrooveFeel DetermineGrooveFeel(TimeSignature meter, float swing, List<RhythmPatternMatch> patterns)
    {
        var mainPattern = patterns.OrderByDescending(p => p.MatchQuality).FirstOrDefault();
        if (mainPattern?.Pattern.Name is not null)
        {
            var name = mainPattern.Pattern.Name;
            if (name.Contains("Tresillo", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Habanera", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Clave", StringComparison.OrdinalIgnoreCase) ||
                (mainPattern.Pattern.Style?.Contains("Latin", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return GrooveFeel.Latin;
            }
        }

        return meter.IsCompound switch
        {
            true => GrooveFeel.Compound,
            _ => swing switch
            {
                >= 0.75f => GrooveFeel.Shuffle,
                > 0.55f and < 0.75f => GrooveFeel.Swing,
                _ => GrooveFeel.Straight
            }
        };
    }

    private static float CalculateGrooveDrive(
        RhythmStatistics stats,
        float density,
        float syncopation,
        float swing,
        GrooveFeel feel)
    {
        var densityNorm = Math.Clamp(density / 2.0f, 0f, 1f);
        var swingBoost = swing is > 0.55f ? 0.05f : 0f;
        var latinBoost = feel == GrooveFeel.Latin ? 0.05f : 0f;

        var strongCount = stats.StrengthHistogram.TryGetValue(BeatStrength.Strong, out var strong)
            ? strong
            : 0;
        var strongEmphasis = stats.TotalNotes > 0 ? strongCount / (float)stats.TotalNotes : 0f;
        var offbeatEmphasis = 1f - strongEmphasis;

        var drive =
            (0.55f * densityNorm) +
            (0.35f * Math.Clamp(syncopation, 0f, 1f)) +
            (0.10f * Math.Clamp(offbeatEmphasis, 0f, 1f)) +
            swingBoost +
            latinBoost;

        return Math.Clamp(drive, 0f, 1f);
    }
}

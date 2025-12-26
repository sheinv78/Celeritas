// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Buffers;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Time signature / meter.
/// </summary>
public readonly struct TimeSignature : IEquatable<TimeSignature>
{
    /// <summary>Beats per measure (numerator).</summary>
    public int BeatsPerMeasure { get; }

    /// <summary>Beat unit as note value (4 = quarter, 8 = eighth, etc).</summary>
    public int BeatUnit { get; }

    /// <summary>Duration of one beat as a Rational.</summary>
    public Rational BeatDuration => new(1, BeatUnit);

    /// <summary>Duration of one measure.</summary>
    public Rational MeasureDuration => new(BeatsPerMeasure, BeatUnit);

    public TimeSignature(int beatsPerMeasure, int beatUnit)
    {
        BeatsPerMeasure = beatsPerMeasure;
        BeatUnit = beatUnit;
    }

    /// <summary>Common time signatures.</summary>
    public static TimeSignature Common => new(4, 4);
    public static TimeSignature CutTime => new(2, 2);
    public static TimeSignature Waltz => new(3, 4);
    public static TimeSignature Compound6 => new(6, 8);
    public static TimeSignature Compound9 => new(9, 8);
    public static TimeSignature Compound12 => new(12, 8);

    /// <summary>Is this a compound meter (beats subdivide into 3)?</summary>
    public bool IsCompound => BeatsPerMeasure is 6 or 9 or 12 && BeatUnit == 8;

    /// <summary>Is this a simple meter (beats subdivide into 2)?</summary>
    public bool IsSimple => !IsCompound;

    /// <summary>Number of strong beats per measure.</summary>
    public int StrongBeats => IsCompound ? BeatsPerMeasure / 3 : BeatsPerMeasure switch
    {
        2 => 1,
        3 => 1,
        4 => 2,
        _ => 1
    };

    public override string ToString() => $"{BeatsPerMeasure}/{BeatUnit}";

    public bool Equals(TimeSignature other) => BeatsPerMeasure == other.BeatsPerMeasure && BeatUnit == other.BeatUnit;
    public override bool Equals(object? obj) => obj is TimeSignature other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(BeatsPerMeasure, BeatUnit);
    public static bool operator ==(TimeSignature left, TimeSignature right) => left.Equals(right);
    public static bool operator !=(TimeSignature left, TimeSignature right) => !left.Equals(right);
}

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
/// A rhythmic event (onset) with metrical position.
/// </summary>
public readonly struct RhythmEvent
{
    public Rational Offset { get; init; }
    public Rational Duration { get; init; }
    public int Measure { get; init; }
    public Rational BeatInMeasure { get; init; }
    public BeatStrength Strength { get; init; }
    public bool IsSyncopated { get; init; }
    public int OriginalIndex { get; init; }

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

    /// <summary>Total duration of the pattern.</summary>
    public Rational TotalDuration => Durations.Aggregate(Rational.Zero, (a, b) => a + b);

    /// <summary>Style/genre association.</summary>
    public string? Style { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    public override string ToString() => Name;
}

/// <summary>
/// Result of meter detection.
/// </summary>
public sealed class MeterDetectionResult
{
    public required TimeSignature TimeSignature { get; init; }
    public required float Confidence { get; init; }
    public required Rational Tempo { get; init; }
    public required IReadOnlyList<TimeSignature> Alternatives { get; init; }
    public required string Reasoning { get; init; }
}

/// <summary>
/// Complete rhythm analysis result.
/// </summary>
public sealed class RhythmAnalysisResult
{
    public required MeterDetectionResult Meter { get; init; }
    public required IReadOnlyList<RhythmEvent> Events { get; init; }
    public required IReadOnlyList<RhythmPatternMatch> PatternMatches { get; init; }
    public required RhythmStatistics Statistics { get; init; }
    public required float SwingRatio { get; init; }
    public required float Syncopation { get; init; }
    public required float Density { get; init; }
    public required string TextureDescription { get; init; }
    
    /// <summary>Detected time signature (convenience accessor to Meter.TimeSignature).</summary>
    public TimeSignature DetectedMeter => Meter.TimeSignature;
    
    /// <summary>Syncopation level expressed as a degree (0-1, same as Syncopation).</summary>
    public float SyncopationDegree => Syncopation;
    
    /// <summary>Count of notes that fall on off-beats or weak positions.</summary>
    public int OffBeatCount => Statistics.StrengthHistogram.TryGetValue(BeatStrength.Weak, out var weak) 
        ? weak 
        : 0;
    
    /// <summary>Percentage of notes on strong beats.</summary>
    public float StrongBeatEmphasis => Statistics.TotalNotes > 0 
        ? (Statistics.StrengthHistogram.TryGetValue(BeatStrength.Strong, out var strong) ? strong : 0) * 100f / Statistics.TotalNotes
        : 0;
}

/// <summary>
/// A matched rhythmic pattern with location.
/// </summary>
public sealed class RhythmPatternMatch
{
    public required RhythmPattern Pattern { get; init; }
    public required Rational StartOffset { get; init; }
    public required int StartIndex { get; init; }
    public required int Count { get; init; }
    public required float MatchQuality { get; init; }
}

/// <summary>
/// Statistics about rhythmic features.
/// </summary>
public sealed class RhythmStatistics
{
    public int TotalNotes { get; init; }
    public int MeasureCount { get; init; }
    public float NotesPerMeasure { get; init; }
    public Rational ShortestDuration { get; init; }
    public Rational LongestDuration { get; init; }
    public Rational AverageDuration { get; init; }
    public int SyncopatedNotes { get; init; }
    public float SyncopationPercent => TotalNotes > 0 ? SyncopatedNotes * 100f / TotalNotes : 0;

    public Dictionary<Rational, int> DurationHistogram { get; init; } = [];
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
    public static MeterDetectionResult DetectMeter(NoteBuffer buffer)
    {
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

        var onsets = new List<(Rational offset, Rational duration, int index)>(buffer.Count);
        for (int i = 0; i < buffer.Count; i++)
        {
            onsets.Add((buffer.GetOffset(i), buffer.GetDuration(i), i));
        }
        onsets.Sort((a, b) => a.offset.CompareTo(b.offset));

        return DetectMeterInternal(onsets);
    }

    /// <summary>
    /// Detect the most likely time signature from a sequence of note events.
    /// </summary>
    public static MeterDetectionResult DetectMeter(IEnumerable<NoteEvent> notes)
    {
        var arr = notes as NoteEvent[] ?? notes.ToArray();
        using var buffer = new NoteBuffer(Math.Max(4, arr.Length));
        buffer.AddRange(arr);
        return DetectMeter(buffer);
    }

    /// <summary>
    /// Identify rhythmic pattern in a sequence of notes.
    /// Returns the best matching pattern with quality score.
    /// </summary>
    public static RhythmPatternMatch? IdentifyPattern(NoteBuffer buffer)
    {
        if (buffer.Count == 0)
            return null;

        var onsets = new List<(Rational offset, Rational duration, int index)>(buffer.Count);
        for (int i = 0; i < buffer.Count; i++)
        {
            onsets.Add((buffer.GetOffset(i), buffer.GetDuration(i), i));
        }
        onsets.Sort((a, b) => a.offset.CompareTo(b.offset));

        var matches = DetectPatterns(onsets);
        return matches.OrderByDescending(m => m.MatchQuality).FirstOrDefault();
    }

    /// <summary>
    /// Identify rhythmic pattern in a sequence of note events.
    /// Returns the best matching pattern with quality score.
    /// </summary>
    public static RhythmPatternMatch? IdentifyPattern(IEnumerable<NoteEvent> notes)
    {
        var arr = notes as NoteEvent[] ?? notes.ToArray();
        using var buffer = new NoteBuffer(Math.Max(4, arr.Length));
        buffer.AddRange(arr);
        return IdentifyPattern(buffer);
    }

    /// <summary>
    /// Analyze rhythm of a note buffer.
    /// </summary>
    public static RhythmAnalysisResult Analyze(NoteBuffer buffer, TimeSignature? knownMeter = null)
    {
        if (buffer.Count == 0)
        {
            return EmptyResult();
        }

        // Collect onsets - pre-allocate exact size
        var onsets = new List<(Rational offset, Rational duration, int index)>(buffer.Count);
        for (int i = 0; i < buffer.Count; i++)
        {
            onsets.Add((buffer.GetOffset(i), buffer.GetDuration(i), i));
        }
        onsets.Sort((a, b) => a.offset.CompareTo(b.offset));

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
            : DetectMeterInternal(onsets);

        // Analyze each event in metrical context
        var events = AnalyzeEvents(onsets, meter.TimeSignature);

        // Detect patterns
        var patterns = DetectPatterns(onsets);

        // Calculate statistics
        var stats = CalculateStatistics(events, meter.TimeSignature);

        // Detect swing
        var swing = DetectSwing(onsets);

        // Calculate syncopation level
        var syncopation = CalculateSyncopation(events);

        // Calculate density
        var density = CalculateDensity(onsets, meter.TimeSignature);

        // Generate texture description
        var texture = DescribeTexture(stats, swing, syncopation, density, patterns);

        return new RhythmAnalysisResult
        {
            Meter = meter,
            Events = events,
            PatternMatches = patterns,
            Statistics = stats,
            SwingRatio = swing,
            Syncopation = syncopation,
            Density = density,
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
        TextureDescription = "No rhythmic content"
    };

    private static MeterDetectionResult DetectMeterInternal(List<(Rational offset, Rational duration, int index)> onsets)
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

        // Find the most common IOI (likely the beat)
        var ioiCounts = new Dictionary<Rational, int>();
        foreach (var ioi in iois)
        {
            ioiCounts.TryGetValue(ioi, out var count);
            ioiCounts[ioi] = count + 1;
        }

        var mostCommonIoi = ioiCounts.OrderByDescending(kv => kv.Value).First().Key;

        // Determine total duration
        var totalDuration = onsets[^1].offset + onsets[^1].duration - onsets[0].offset;

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
            scores[m] = ScoreMeter(onsets, m);
        }

        var best = scores.OrderByDescending(kv => kv.Value).First();
        var alternatives = scores
            .Where(kv => kv.Key != best.Key && kv.Value > 0.3f)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .Take(3)
            .ToList();

        var reasoning = best.Key.IsCompound
            ? $"Compound meter detected - notes group in threes"
            : $"Simple meter - beats divide in two";

        if (best.Value < 0.5f)
            reasoning += " (low confidence)";

        return new MeterDetectionResult
        {
            TimeSignature = best.Key,
            Confidence = best.Value,
            Tempo = new Rational(120, 1), // Would need audio for real tempo
            Alternatives = alternatives,
            Reasoning = reasoning
        };
    }

    private static float ScoreMeter(List<(Rational offset, Rational duration, int index)> onsets, TimeSignature meter)
    {
        float score = 0;
        var measureDur = meter.MeasureDuration;

        foreach (var (offset, duration, _) in onsets)
        {
            // Calculate position within measure
            var measurePos = GetPositionInMeasure(offset, measureDur);
            var strength = GetBeatStrength(measurePos, meter);

            // Reward notes on strong beats
            score += strength switch
            {
                BeatStrength.Strong => 1.0f,
                BeatStrength.Medium => 0.6f,
                BeatStrength.Weak => 0.3f,
                BeatStrength.Subdivision => 0.1f,
                _ => 0.1f
            };
        }

        return score / onsets.Count;
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

        // Beat 0 (downbeat) is always strong
        if (beat == 0)
            return BeatStrength.Strong;

        // In 4/4, beat 2 (third beat) is medium
        if (meter.BeatsPerMeasure == 4 && beat == 2)
            return BeatStrength.Medium;

        // In compound meters, beats 0, 3, 6, 9 are strong
        if (meter.IsCompound && beat % 3 == 0)
            return beat == 0 ? BeatStrength.Strong : BeatStrength.Medium;

        return BeatStrength.Weak;
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

            // Detect syncopation: note on weak beat that's longer than surrounding or ties over strong beat
            var isSyncopated = false;
            if (strength is BeatStrength.Weak or BeatStrength.Subdivision)
            {
                var noteEnd = offset + duration;
                var nextBeat = GetNextStrongBeat(offset, meter);
                if (noteEnd > nextBeat)
                    isSyncopated = true;
            }

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
        var currentBeat = (long)(offset.ToDouble() / beatDur.ToDouble());
        return beatDur * (currentBeat + 1);
    }

    private static List<RhythmPatternMatch> DetectPatterns(
        List<(Rational offset, Rational duration, int index)> onsets)
    {
        // Estimate: patterns are typically 4-8 notes, not many matches expected
        var matches = new List<RhythmPatternMatch>(onsets.Count / 4);

        foreach (var pattern in CommonPatterns)
        {
            // Slide pattern over onsets
            for (int i = 0; i <= onsets.Count - pattern.Durations.Length; i++)
            {
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

        // Remove overlapping matches, keep best
        matches = matches
            .GroupBy(m => m.StartIndex / 4) // Group by approximate position
            .Select(g => g.OrderByDescending(m => m.MatchQuality).First())
            .ToList();

        return matches;
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
        return Math.Max(0, 1.0f - avgError * 4);
    }

    private static RhythmStatistics CalculateStatistics(
        List<RhythmEvent> events,
        TimeSignature meter)
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
        var avgDur = new Rational(
            durations.Sum(d => d.Numerator) / durations.Count,
            durations.First().Denominator);

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
        // Look for pairs of notes that should be equal but aren't
        // Swing = ratio of first to second in a pair
        var pairs = new List<(double first, double second)>();

        for (int i = 0; i < onsets.Count - 1; i += 2)
        {
            var d1 = onsets[i].duration.ToDouble();
            var d2 = i + 1 < onsets.Count ? onsets[i + 1].duration.ToDouble() : d1;

            // Only consider pairs that sum to roughly a beat
            if (Math.Abs(d1 + d2 - 0.5) < 0.1 || Math.Abs(d1 + d2 - 0.25) < 0.05)
            {
                pairs.Add((d1, d2));
            }
        }

        if (pairs.Count == 0)
            return 0.5f; // No swing detected (straight)

        var avgRatio = pairs.Average(p => p.first / (p.first + p.second));
        return (float)avgRatio;
    }

    private static float CalculateSyncopation(List<RhythmEvent> events)
    {
        if (events.Count == 0) return 0;
        return events.Count(e => e.IsSyncopated) / (float)events.Count;
    }

    private static float CalculateDensity(
        List<(Rational offset, Rational duration, int index)> onsets,
        TimeSignature meter)
    {
        if (onsets.Count < 2) return 0;

        var totalDuration = onsets[^1].offset + onsets[^1].duration - onsets[0].offset;
        var measures = totalDuration.ToDouble() / meter.MeasureDuration.ToDouble();

        return (float)(onsets.Count / measures / meter.BeatsPerMeasure);
    }

    private static string DescribeTexture(
        RhythmStatistics stats,
        float swing,
        float syncopation,
        float density,
        List<RhythmPatternMatch> patterns)
    {
        var parts = new List<string>();

        // Density description
        parts.Add(density switch
        {
            < 0.5f => "Sparse, spacious rhythm",
            < 1.0f => "Moderate rhythmic activity",
            < 2.0f => "Active, driving rhythm",
            _ => "Dense, busy rhythmic texture"
        });

        // Swing description
        if (swing is > 0.55f and < 0.75f)
            parts.Add($"with light swing ({swing:P0} ratio)");
        else if (swing >= 0.75f)
            parts.Add($"with heavy swing/shuffle ({swing:P0} ratio)");

        // Syncopation
        if (syncopation > 0.3f)
            parts.Add($"highly syncopated ({syncopation:P0})");
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
}
